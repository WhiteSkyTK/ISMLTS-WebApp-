using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ISMLTS.Tests
{
    // Small real (and fake) files for upload tests
    public static class TestFiles
    {
        public static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

        public static byte[] Docx() => Zip(("[Content_Types].xml", "<Types/>"), ("word/document.xml", "<w:document/>"));

        public static byte[] Zip(params (string Name, string Content)[] entries)
        {
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, content) in entries)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                    writer.Write(content);
                }
            }
            return buffer.ToArray();
        }

        // What a Windows program looks like: "MZ" and nothing a PDF reader would accept
        public static byte[] Program() => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00];

        public static IFormFile Upload(byte[] content, string fileName) =>
            new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
    }
}
