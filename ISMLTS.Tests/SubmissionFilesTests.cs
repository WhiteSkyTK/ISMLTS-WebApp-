using System.Text;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class SubmissionFilesTests
    {
        private const long Limit = 20 * 1024 * 1024;

        private static FileCheck Check(byte[] content, string name, long? length = null) =>
            SubmissionFileRules.Check(new MemoryStream(content), name, length ?? content.Length, Limit);

        [Theory]
        [InlineData("work.pdf", ".pdf", "application/pdf")]
        [InlineData("WORK.PDF", ".pdf", "application/pdf")]
        public void RealPdf_IsAccepted(string name, string extension, string contentType)
        {
            var check = Check(TestFiles.Pdf(), name);

            Assert.True(check.Ok);
            Assert.Equal((extension, contentType), (check.Extension, check.ContentType));
        }

        [Fact]
        public void RealDocx_AndZip_AreAccepted()
        {
            Assert.True(Check(TestFiles.Docx(), "essay.docx").Ok);
            Assert.True(Check(TestFiles.Zip(("src/Program.cs", "class P {}")), "project.zip").Ok);
            // A Word document is a ZIP inside, so it may also be handed in as one
            Assert.True(Check(TestFiles.Docx(), "essay.zip").Ok);
        }

        [Fact]
        public void TheContentDecides_NotTheName()
        {
            Assert.Contains("isn't a real PDF", Check(TestFiles.Program(), "work.pdf").Error);
            Assert.Contains("isn't a real PDF", Check(TestFiles.Docx(), "work.pdf").Error);
            Assert.Contains("isn't a real Word document", Check(TestFiles.Pdf(), "work.docx").Error);
            // A ZIP that isn't a Word document, renamed .docx
            Assert.Contains("isn't a real Word document", Check(TestFiles.Zip(("notes.txt", "hi")), "work.docx").Error);
            Assert.Contains("isn't a real ZIP file", Check(TestFiles.Pdf(), "work.zip").Error);
            Assert.Contains("isn't a real ZIP file", Check(TestFiles.Zip(), "empty.zip").Error);
            Assert.Contains("isn't a real PDF", Check("%PD"u8.ToArray(), "short.pdf").Error);
        }

        [Theory]
        [InlineData("setup.exe")]
        [InlineData("notes.txt")]
        [InlineData("work")]
        [InlineData("work.pdf.exe")]
        public void OtherKindsOfFile_AreRefused(string name)
        {
            Assert.Equal(SubmissionFileRules.WrongType, Check(TestFiles.Pdf(), name).Error);
        }

        [Fact]
        public void EmptyAndOversizedFiles_AreRefused()
        {
            Assert.Equal("That file is empty.", Check([], "work.pdf").Error);
            Assert.Equal("That file is too big. Files can be up to 20 MB.", Check(TestFiles.Pdf(), "work.pdf", Limit + 1).Error);
        }

        [Fact]
        public void MaxFileMegabytes_StaysWithinWhatAppServiceAccepts()
        {
            Assert.Equal(25, new SubmissionFileOptions { MaxFileMegabytes = 500 }.Megabytes);
            Assert.Equal(1, new SubmissionFileOptions { MaxFileMegabytes = 0 }.Megabytes);
            Assert.Equal(20L * 1024 * 1024, new SubmissionFileOptions().MaxBytes);
        }

        [Theory]
        [InlineData("C:\\Users\\me\\My Work.pdf", ".pdf", "My Work.pdf")]
        [InlineData("../../etc/passwd.pdf", ".pdf", "passwd.pdf")]
        [InlineData("a<b>:c|d?.pdf", ".pdf", "a_b__c_d_.pdf")]
        [InlineData("report.PDF", ".pdf", "report.pdf")]
        [InlineData(".pdf", ".pdf", "submission.pdf")]
        [InlineData(null, ".zip", "submission.zip")]
        public void DisplayName_IsSafeToUseAsADownloadName(string? uploaded, string extension, string expected)
        {
            Assert.Equal(expected, SubmissionFileRules.DisplayName(uploaded, extension));
        }

        [Fact]
        public void DisplayName_IsCutToAReasonableLength()
        {
            var name = SubmissionFileRules.DisplayName(new string('x', 300) + ".pdf", ".pdf");

            Assert.Equal(104, name.Length);
            Assert.EndsWith(".pdf", name);
        }

        [Fact]
        public void StoredNames_AreGenerated_AndNeverRepeat()
        {
            var first = SubmissionFileRules.NewStoredName(12, ".pdf");
            var second = SubmissionFileRules.NewStoredName(12, ".pdf");

            Assert.Matches(new Regex("^12/[0-9a-f]{32}\\.pdf$", RegexOptions.None, TimeSpan.FromSeconds(1)), first);
            Assert.NotEqual(first, second);
        }

        [Theory]
        [InlineData(500, "500 B")]
        [InlineData(2048, "2 KB")]
        [InlineData(5 * 1024 * 1024 + 300 * 1024, "5.3 MB")]
        public void Size_IsReadable(long bytes, string expected)
        {
            Assert.Equal(expected, SubmissionFileRules.Size(bytes));
        }

        [Theory]
        [InlineData(null, 40, true)]
        [InlineData(0, 0, true)]
        [InlineData(0, 1, false)]
        [InlineData(3, 3, true)]
        [InlineData(3, 4, false)]
        public void Window_ClosesAfterTheLateDays(int? lateDays, int daysAfterDue, bool open)
        {
            var due = new DateTime(2026, 10, 10);

            Assert.Equal(open, SubmissionWindow.IsOpen(due, lateDays, due.AddDays(daysAfterDue)));
        }

        [Fact]
        public void Window_IsDescribedForTheStudent()
        {
            var due = new DateTime(2026, 10, 10);

            Assert.StartsWith("Work handed in after the due date is still accepted", SubmissionWindow.Describe(due, null, due));
            Assert.Equal("Submissions close at the end of the due date (Sat, 10 Oct).", SubmissionWindow.Describe(due, 0, due));
            Assert.Equal("Late work is accepted until the end of Tue, 13 Oct.", SubmissionWindow.Describe(due, 3, due));
            Assert.Equal("Submissions closed at the end of Tue, 13 Oct 2026.", SubmissionWindow.Describe(due, 3, due.AddDays(4)));
        }

        [Fact]
        public async Task LocalStore_SavesOpensAndDeletes_InsideItsFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ismlts-tests", Guid.NewGuid().ToString("N"));
            var store = new LocalFileStore(folder);
            try
            {
                await store.SaveAsync("7/abc.pdf", new MemoryStream(TestFiles.Pdf()), "application/pdf");

                var download = await store.OpenAsync("7/abc.pdf", "application/pdf", "work.pdf");
                Assert.Null(download?.RedirectTo);
                using (var reader = new StreamReader(download!.Content!, Encoding.ASCII))
                {
                    Assert.StartsWith("%PDF-", await reader.ReadToEndAsync());
                }

                // Stored names are never reused, so saving over a file fails
                await Assert.ThrowsAsync<IOException>(() => store.SaveAsync("7/abc.pdf", new MemoryStream(TestFiles.Pdf()), "application/pdf"));
                await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("../outside.pdf", new MemoryStream(TestFiles.Pdf()), "application/pdf"));

                await store.DeleteAsync("7/abc.pdf");
                await store.DeleteAsync("7/abc.pdf");
                Assert.Null(await store.OpenAsync("7/abc.pdf", "application/pdf", "work.pdf"));
                Assert.True(await store.CanConnectAsync());
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
        }
    }
}
