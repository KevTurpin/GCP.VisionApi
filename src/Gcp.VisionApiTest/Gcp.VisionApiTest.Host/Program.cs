using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Cloud.Storage.V1;
using Google.Cloud.Vision.V1;
using Google.Protobuf;

namespace Gcp.VisionApiTest.Host
{
    class Program
    {
        static void Main(string[] args)
        {
            //DoAnImageLocally();
            DoAnImageInCloudStorage();
            
            Console.ReadLine();
        }


        static void DoAnImageLocally()
        {
            // Instantiates a client
            var client = ImageAnnotatorClient.Create();
            // Load the image file into memory
            var telkomBillImage = Image.FromFile(@"assets\TestTelkomBill.jpg");

            // Performs label detection on the image file
            var response = client.DetectLabels(telkomBillImage);

            foreach (var annotation in response)
            {
                if (annotation.Description != null)
                    Console.WriteLine(annotation.Description);
            }
            //Console.WriteLine

            var text = client.DetectText(telkomBillImage).ToArray();
            var b = client.DetectImageProperties(telkomBillImage);
            
            var logos = client.DetectLogos(telkomBillImage).ToArray();
            var docText = client.DetectDocumentText(telkomBillImage);
        }
        static void DoAnImageInCloudStorage()
        {
            // Specify a Google Cloud Storage uri for the image
            // or a publicly accessible HTTP or HTTPS uri.

            //var uri = @"https://storage.cloud.google.com/iocotechmarch2020poc/imgs/sign_text.png";
            var uri = @"gs://iocotechmarch2020poc/imgs/sign_text.png";

            var image = Image.FromUri(uri);
            var client = ImageAnnotatorClient.Create();
            var response = client.DetectText(image);
            foreach (var annotation in response)
            {
                if (annotation.Description != null)
                    Console.WriteLine(annotation.Description);
            }
        }

        private static object DetectDocument(
            string gcsSourceUri,
            string gcsDestinationBucketName, 
            string gcsDestinationPrefixName)
        {
            var client = ImageAnnotatorClient.Create();

            var asyncRequest = new AsyncAnnotateFileRequest
            {
                InputConfig = new InputConfig
                {
                    GcsSource = new GcsSource
                    {
                        Uri = gcsSourceUri
                    },
                    // Supported mime_types are: 'application/pdf' and 'image/tiff'
                    MimeType = "application/pdf"
                },
                OutputConfig = new OutputConfig
                {
                    // How many pages should be grouped into each json output file.
                    BatchSize = 2,
                    GcsDestination = new GcsDestination
                    {
                        Uri = $"gs://{gcsDestinationBucketName}/{gcsDestinationPrefixName}"
                    }
                }
            };

            asyncRequest.Features.Add(new Feature
            {
                Type = Feature.Types.Type.DocumentTextDetection
            });

            List<AsyncAnnotateFileRequest> requests =
                new List<AsyncAnnotateFileRequest>();
            requests.Add(asyncRequest);

            var operation = client.AsyncBatchAnnotateFiles(requests);

            Console.WriteLine("Waiting for the operation to finish");

            operation.PollUntilCompleted();

            // Once the rquest has completed and the output has been
            // written to GCS, we can list all the output files.
            var storageClient = StorageClient.Create();

            // List objects with the given prefix.
            var blobList = storageClient.ListObjects(gcsDestinationBucketName,
                gcsDestinationPrefixName);
            Console.WriteLine("Output files:");
            foreach (var blob in blobList)
            {
                Console.WriteLine(blob.Name);
            }

            // Process the first output file from GCS.
            // Select the first JSON file from the objects in the list.
            var output = blobList.Where(x => x.Name.Contains(".json")).First();

            var jsonString = "";
            using (var stream = new MemoryStream())
            {
                storageClient.DownloadObject(output, stream);
                jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }

            var response = JsonParser.Default
                        .Parse<AnnotateFileResponse>(jsonString);

            // The actual response for the first page of the input file.
            var firstPageResponses = response.Responses[0];
            var annotation = firstPageResponses.FullTextAnnotation;

            // Here we print the full text from the first page.
            // The response contains more information:
            // annotation/pages/blocks/paragraphs/words/symbols
            // including confidence scores and bounding boxes
            Console.WriteLine($"Full text: \n {annotation.Text}");

            return 0;
        }
    }
}
