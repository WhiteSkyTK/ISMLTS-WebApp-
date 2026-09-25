using QRCoder;

namespace ISMLTS_WebApp_.Services
{
    public interface IQrCodeService
    {
        string ToPngDataUri(string text);
    }

    public class QrCodeService : IQrCodeService
    {
        public string ToPngDataUri(string text)
        {
            using var generator = new QRCodeGenerator();
            using var qrCode = new PngByteQRCode(generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q));
            return "data:image/png;base64," + Convert.ToBase64String(qrCode.GetGraphic(10));
        }
    }
}