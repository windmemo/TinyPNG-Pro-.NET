using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using TinyPNGPro.Core.Logging;
using TinyPNGPro.Core.Platform;

namespace TinyPNGPro.Core.TinyPng
{
    public sealed class CompressionResult
    {
        public long OriginalSize;
        public long CompressedSize;
        public double Ratio;
        public int CompressionCount;
    }

    /// <summary>
    /// TinyPNG 在线压缩客户端：POST /shrink 上传 -> Location GET 下载。
    /// 行为对齐 Python 版：Basic api:key、有限重试、错误映射、额度解析、1x1 PNG 验证。
    /// </summary>
    public sealed class TinyPngClient
    {
        private const string ApiUrl = "https://api.tinify.com/shrink";
        private const int MaxRetries = 2;

        // 1x1 PNG（用于验证 API Key：400 视为密钥有效）
        private const string ProbePngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

        private static readonly Dictionary<string, HttpClient> Clients = new Dictionary<string, HttpClient>();
        private static readonly object ClientsLock = new object();

        private readonly string _apiKey;

        public TinyPngClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        private HttpClient GetClient()
        {
            lock (ClientsLock)
            {
                HttpClient client = Clients.ContainsKey(_apiKey) ? Clients[_apiKey] : null;
                if (client != null)
                {
                    return client;
                }

                HttpClientHandler handler = new HttpClientHandler();
                handler.AllowAutoRedirect = true;
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                handler.UseDefaultCredentials = false;

                client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(300);

                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes("api:" + _apiKey));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
                Clients[_apiKey] = client;
                return client;
            }
        }

        public CompressionResult CompressFile(string inputPath, string outputPath, int timeoutSeconds)
        {
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Input file not found: " + inputPath);
            }

            long originalSize = new FileInfo(inputPath).Length;
            byte[] payload = File.ReadAllBytes(inputPath);
            byte[] compressed;
            int compressionCount = 0;

            HttpResponseMessage shrinkResponse = null;
            try
            {
                shrinkResponse = ExecuteWithRetry(
                    HttpMethod.Post, ApiUrl, payload, "application/octet-stream", timeoutSeconds);

                if (!shrinkResponse.IsSuccessStatusCode)
                {
                    ThrowForStatus(shrinkResponse.StatusCode, inputPath);
                }

                compressionCount = ParseCompressionCount(shrinkResponse);

                Uri location = shrinkResponse.Headers.Location;
                if (location != null)
                {
                    string downloadUrl = location.IsAbsoluteUri
                        ? location.AbsoluteUri
                        : new Uri(new Uri(ApiUrl), location).AbsoluteUri;

                    HttpResponseMessage downloadResponse = ExecuteWithRetry(
                        HttpMethod.Get, downloadUrl, null, null, timeoutSeconds);
                    try
                    {
                        if (!downloadResponse.IsSuccessStatusCode)
                        {
                            ThrowForStatus(downloadResponse.StatusCode, inputPath);
                        }
                        compressed = ReadContent(downloadResponse);
                    }
                    finally
                    {
                        downloadResponse.Dispose();
                    }
                }
                else
                {
                    compressed = ReadContent(shrinkResponse);
                }
            }
            catch (TimeoutException)
            {
                AppLogger.Error("Compression timed out for: " + inputPath);
                throw new TimeoutException("Compression request timed out");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Compression failed for " + inputPath + ": " + ex.Message);
                throw;
            }
            finally
            {
                if (shrinkResponse != null)
                {
                    shrinkResponse.Dispose();
                }
            }

            PathUtil.EnsureDirectory(outputPath);
            File.WriteAllBytes(outputPath, compressed);

            CompressionResult result = new CompressionResult();
            result.OriginalSize = originalSize;
            result.CompressedSize = compressed.Length;
            result.Ratio = originalSize > 0 ? 1.0 - (double)compressed.Length / (double)originalSize : 0.0;
            result.CompressionCount = compressionCount;
            return result;
        }

        private static byte[] ReadContent(HttpResponseMessage response)
        {
            using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
        }

        private static void ThrowForStatus(HttpStatusCode status, string path)
        {
            int code = (int)status;
            string message;
            if (code == 401)
            {
                message = "Invalid API key";
            }
            else if (code == 429)
            {
                message = "API rate limit exceeded";
            }
            else if (code == 400)
            {
                message = "Invalid image file";
            }
            else if (code == 500)
            {
                message = "Server error";
            }
            else
            {
                message = "HTTP error " + code.ToString();
            }
            AppLogger.Error(message + " for: " + path);
            throw new Exception(message);
        }

        private HttpResponseMessage ExecuteWithRetry(
            HttpMethod method, string url, byte[] body, string contentType, int timeoutSeconds)
        {
            Exception lastError = null;
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    using (CancellationTokenSource cts =
                        new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                    {
                        HttpRequestMessage request = new HttpRequestMessage(method, url);
                        if (body != null && method == HttpMethod.Post)
                        {
                            ByteArrayContent content = new ByteArrayContent(body);
                            if (contentType != null)
                            {
                                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                            }
                            request.Content = content;
                        }

                        HttpResponseMessage response =
                            GetClient().SendAsync(request, cts.Token).GetAwaiter().GetResult();

                        if (response == null)
                        {
                            throw new HttpRequestException("Empty response");
                        }
                        if (IsRetryableStatus(response.StatusCode) && attempt < MaxRetries)
                        {
                            response.Dispose();
                            SleepBackoff(attempt);
                            continue;
                        }
                        return response;
                    }
                }
                catch (OperationCanceledException)
                {
                    lastError = new TimeoutException("Compression request timed out");
                    if (attempt < MaxRetries)
                    {
                        SleepBackoff(attempt);
                        continue;
                    }
                    throw new TimeoutException("Compression request timed out");
                }
                catch (HttpRequestException ex)
                {
                    lastError = ex;
                    if (attempt < MaxRetries)
                    {
                        SleepBackoff(attempt);
                        continue;
                    }
                    throw new Exception("Network connection failed");
                }
            }
            throw new Exception("Network connection failed", lastError);
        }

        private static bool IsRetryableStatus(HttpStatusCode status)
        {
            int code = (int)status;
            return code == 429 || code == 500 || code == 502 || code == 503 || code == 504;
        }

        private static void SleepBackoff(int attempt)
        {
            try
            {
                Thread.Sleep((int)(500 * Math.Pow(2, attempt)));
            }
            catch
            {
            }
        }

        private static int ParseCompressionCount(HttpResponseMessage response)
        {
            if (response == null)
            {
                return 0;
            }
            try
            {
                IEnumerable<string> values;
                if (response.Headers.TryGetValues("Compression-Count", out values))
                {
                    foreach (string raw in values)
                    {
                        string text = raw == null ? string.Empty : raw.Trim();
                        int value;
                        if (int.TryParse(text, out value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Failed to parse Compression-Count header: " + ex.Message);
            }
            return 0;
        }

        /// <summary>验证 Key：1x1 PNG 试请求，400 视为密钥有效（与 Python 版一致）。</summary>
        public bool ValidateApiKey()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return false;
            }
            try
            {
                byte[] probe = Convert.FromBase64String(ProbePngBase64);
                HttpResponseMessage response = ExecuteWithRetry(
                    HttpMethod.Post, ApiUrl, probe, "image/png", 10);
                try
                {
                    int code = (int)response.StatusCode;
                    return code == 400;
                }
                finally
                {
                    response.Dispose();
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
