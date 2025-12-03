using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.Runtime;

namespace AISoundGenerator.Services;

public class WasabiStorageService
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly string _region;

    public WasabiStorageService(string accessKey, string secretKey, string region, string bucketName)
    {
        _bucketName = bucketName;
        _region = region;

        // Configure the S3 client to use Wasabi - fixing the service URL format
        // Wasabi requires a specific URL format
        var config = new AmazonS3Config
        {
            ServiceURL = GetServiceUrl(region),
            ForcePathStyle = true, // Required for Wasabi
            UseHttp = false, // Use HTTPS
            SignatureVersion = "4", // Use signature version 4
            SignatureMethod = SigningAlgorithm.HmacSHA256 // Use SHA256
        };

        Debug.WriteLine($"Using Wasabi service URL: {config.ServiceURL}");

        try
        {
            // Create the S3 client with Wasabi credentials
            _client = new AmazonS3Client(
                new BasicAWSCredentials(accessKey, secretKey),
                config
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating Wasabi client: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the correct service URL for the given Wasabi region
    /// </summary>
    private string GetServiceUrl(string region)
    {
        // Wasabi's endpoint formats changed over time, this ensures we use the correct one
        switch (region.ToLower())
        {
            case "us-east-1":
                return "https://s3.wasabisys.com"; // Legacy URL for us-east-1
            case "us-east-2":
                return "https://s3.us-east-2.wasabisys.com";
            case "us-central-1":
                return "https://s3.us-central-1.wasabisys.com";
            case "us-west-1":
                return "https://s3.us-west-1.wasabisys.com";
            case "eu-central-1":
                return "https://s3.eu-central-1.wasabisys.com";
            case "eu-central-2":
                return "https://s3.eu-central-2.wasabisys.com";
            case "eu-west-1":
                return "https://s3.eu-west-1.wasabisys.com";
            case "eu-west-2":
                return "https://s3.eu-west-2.wasabisys.com";
            case "ap-northeast-1":
                return "https://s3.ap-northeast-1.wasabisys.com";
            case "ap-northeast-2":
                return "https://s3.ap-northeast-2.wasabisys.com";
            case "ca-central-1":
                return "https://s3.ca-central-1.wasabisys.com";
            default:
                return $"https://s3.{region}.wasabisys.com"; // For any other regions
        }
    }

    /// <summary>
    /// Uploads a file to Wasabi storage
    /// </summary>
    /// <param name="filePath">Local file path to upload</param>
    /// <param name="objectKey">Optional object key (file name in bucket). If null, the file name will be used</param>
    /// <returns>URL of the uploaded file</returns>
    public async Task<string> UploadFileAsync(string filePath, string? objectKey = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        // If no object key provided, use the filename
        objectKey ??= Path.GetFileName(filePath);

        // Create a folder structure based on date, using only year and month
        string folderPrefix = DateTime.Now.ToString("yyyy/MM/");
        string fullObjectKey = $"{folderPrefix}{objectKey}";

        Debug.WriteLine($"Uploading file: {filePath}");
        Debug.WriteLine($"To Wasabi bucket: {_bucketName}");
        Debug.WriteLine($"Object key: {fullObjectKey}");

        try
        {
            // First, verify bucket exists
            try
            {
                var bucketResponse = await _client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    MaxKeys = 1
                });

                Debug.WriteLine($"Successfully connected to bucket: {_bucketName}");
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"S3 error: {ex.Message}");
                Debug.WriteLine($"Error code: {ex.ErrorCode}");
                Debug.WriteLine($"Status code: {ex.StatusCode}");
                Debug.WriteLine($"Request ID: {ex.RequestId}");

                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"Bucket does not exist: {_bucketName}");
                    throw new Exception($"Bucket does not exist: {_bucketName}", ex);
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Debug.WriteLine("Authentication failed - check access key and secret key");
                    throw new Exception("Authentication failed - check your Wasabi credentials", ex);
                }
                else
                {
                    Debug.WriteLine($"Error checking bucket: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"General error checking bucket: {ex.Message}");
                throw;
            }

            // Use direct PutObjectRequest instead of TransferUtility for better control
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fullObjectKey,
                InputStream = fileStream,
                ContentType = GetMimeType(filePath),
                CannedACL = S3CannedACL.PublicRead
            };

            // Upload the file
            await _client.PutObjectAsync(putRequest);

            Debug.WriteLine($"Upload completed successfully");

            // Verify the upload was successful by trying to get the object metadata
            try
            {
                var metadata = await _client.GetObjectMetadataAsync(_bucketName, fullObjectKey);
                Debug.WriteLine($"Object exists in bucket with size: {metadata.ContentLength} bytes");
            }
            catch (AmazonS3Exception ex)
            {
                Debug.WriteLine($"Error verifying upload: {ex.Message}");
                throw new Exception("Upload appeared to succeed but object is not accessible", ex);
            }

            // Return the URL to the uploaded file
            string url;

            // Construct a proper Wasabi URL based on region
            if (_region.ToLower() == "us-east-1")
            {
                // US East 1 has a different URL format
                url = $"https://s3.wasabisys.com/{_bucketName}/{fullObjectKey}";
            }
            else
            {
                url = $"https://s3.{_region}.wasabisys.com/{_bucketName}/{fullObjectKey}";
            }

            Debug.WriteLine($"File public URL: {url}");
            return url;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Upload failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Gets the total size in bytes of all files in a specific folder
    /// </summary>
    /// <param name="folderPrefix">The folder prefix to check (e.g., "2024/01/")</param>
    /// <returns>Total size in bytes</returns>
    public async Task<long> GetFolderSizeAsync(string folderPrefix)
    {
        long totalSize = 0;
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = folderPrefix
            };

            ListObjectsV2Response response;
            do
            {
                response = await _client.ListObjectsV2Async(request);
                
                foreach (var obj in response.S3Objects)
                {
                    totalSize += obj.Size;
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

            Debug.WriteLine($"Total size for folder {folderPrefix}: {totalSize} bytes");
            return totalSize;
        }
        catch (AmazonS3Exception ex)
        {
            Debug.WriteLine($"Error getting folder size: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the MIME type for a file based on its extension
    /// </summary>
    private string GetMimeType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();

        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            _ => "application/octet-stream" // Default binary type
        };
    }
}