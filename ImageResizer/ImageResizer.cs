using System;
using System.IO;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageResizer
{
    public static class ResizeAndStore
    {

        const string QueueConnectionString = "QueueConnectionString";
        const string QueueName = "swl2";
        const string BlobStorageConnectionString = "BlobStorageConnectionString";
        const string BlobStorageName = "swi2imagestorage";

        [FunctionName("ResizeAndStore")]
        public static void Run(
            [ServiceBusTrigger(QueueName, Connection = QueueConnectionString)] string queueItem,
            [Blob(BlobStorageName, FileAccess.ReadWrite, Connection = BlobStorageConnectionString)] CloudBlobContainer blobContainer,
            ILogger log
            )
        {
            var image = new MemoryStream(Convert.FromBase64String(queueItem));
            
            blobContainer.CreateIfNotExists();

            new ImageHandler(blobContainer, log, image)
                .Upload("original", Scale.Original)
                .Upload("large", Scale.Large)
                .Upload("small", Scale.Small);
        }

        class ImageHandler
        {

            private readonly CloudBlobContainer blobContainer;
            private readonly ILogger logger;
            private readonly Stream image;
            private readonly Guid id;
            private readonly Func<string, string> nameFunction;

            public ImageHandler(CloudBlobContainer blobContainer, ILogger logger, Stream image)
            {
                this.blobContainer = blobContainer;
                this.logger = logger;
                this.image = image;
                this.id = Guid.NewGuid();
                this.nameFunction = suffix => $"{id}_{suffix}.jpg";
            }

            public ImageHandler Upload(string description, double scaleFactor)
            {
                var name = nameFunction.Invoke(description);
                var cloudBlockBlob = blobContainer.GetBlockBlobReference(name);
                var imageStream = CreateMemoryStream(image, scaleFactor);
                cloudBlockBlob.UploadFromStream(imageStream);

                logger.LogInformation($"Successfully uploaded files to blob storage: {name}");
                return this;
            }

            private MemoryStream CreateMemoryStream(Stream image, double scaleFactor)
            {
                var ms = new MemoryStream();
                var img = Image.FromStream(image);
                var desiredWidth = img.Width * scaleFactor;
                var ratio = (decimal) desiredWidth / img.Width;
                var resized = ResizeImage(img, (int) desiredWidth, (int) Math.Floor((img.Height * ratio)));
                resized.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                return ms;
            }

            private Bitmap ResizeImage(Image image, int width, int height)
            {
                var destRect = new Rectangle(0, 0, width, height);
                var destImage = new Bitmap(width, height);

                destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using var wrapMode = new ImageAttributes();
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }

                return destImage;
            }

        }

        private struct Scale
        {
            public const double Original = 1.0;
            public const double Large = 2.0;
            public const double Small = 0.25;
        }
    }
}
