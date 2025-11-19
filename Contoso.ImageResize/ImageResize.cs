using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Contoso.ImageResize
{
    public static class ImageResize
    {
        [FunctionName("ImageResize")]
        public static void Run(
            [BlobTrigger("images/{name}", Connection = "AzureWebJobsStorage")] Stream inputBlob,
            string name,
            [Blob("thumbnails/{name}", FileAccess.Write, Connection = "AzureWebJobsStorage")] Stream outputBlob,
            ILogger log)
        {
            log.LogInformation($"Blob trigger fired for '{name}' with size {inputBlob.Length} bytes");

            try
            {
                // Load the image from the input blob
                using var image = Image.Load(inputBlob);

                // Resize to a maximum of 150x150 while preserving aspect ratio
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(100, 100)
                }));

                // Save the resized image to the output blob in JPEG format
                image.Save(outputBlob, new JpegEncoder());

                log.LogInformation($"Thumbnail created for '{name}' and saved to 'thumbnails' container");
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing blob '{name}': {ex.Message}");
            }
        }
    }
}