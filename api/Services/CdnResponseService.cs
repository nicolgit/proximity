using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace api.Services
{
    /// <summary>
    /// Service for configuring HTTP responses to be CDN-friendly with proper caching headers
    /// </summary>
    public class CdnResponseService
    {
        /// <summary>
        /// Cache duration for all cacheable API responses
        /// </summary>
        public static class CacheDurations
        {
            public const int ApiCache = 3600;          // 1 hour - unified cache duration for all cacheable APIs
        }

        /// <summary>
        /// Configures response with CDN-friendly headers for cacheable data
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <param name="content">Response content for ETag generation</param>
        /// <param name="lastModified">Optional last modified date</param>
        /// <param name="maxAge">Cache duration in seconds</param>
        public static void ConfigureCacheableResponse(HttpResponse response, object content, 
            DateTime? lastModified = null, int maxAge = CacheDurations.ApiCache)
        {
            // Generate ETag from content
            var etag = GenerateETag(content);
            
            // Set cache headers
            response.Headers["Cache-Control"] = $"public, max-age={maxAge}, s-maxage={maxAge}";
            response.Headers["ETag"] = etag;
            
            if (lastModified.HasValue)
            {
                response.Headers["Last-Modified"] = lastModified.Value.ToString("R");
            }
            
            // CDN optimization headers
            response.Headers["Vary"] = "Accept-Encoding";
            
            // Enable compression hints for CDN
            response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        /// <summary>
        /// Configures response with CDN-friendly headers for isochrone data
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <param name="content">Response content for ETag generation</param>
        /// <param name="lastModified">Optional last modified date</param>
        public static void ConfigureIsochroneResponse(HttpResponse response, string content, 
            DateTime? lastModified = null)
        {
            var etag = GenerateETag(content);
            
            // Use unified cache duration for isochrone data
            response.Headers["Cache-Control"] = $"public, max-age={CacheDurations.ApiCache}, s-maxage={CacheDurations.ApiCache}";
            response.Headers["ETag"] = etag;
            
            if (lastModified.HasValue)
            {
                response.Headers["Last-Modified"] = lastModified.Value.ToString("R");
            }
            
            // CDN optimization headers
            response.Headers["Vary"] = "Accept-Encoding";
            response.Headers["X-Content-Type-Options"] = "nosniff";
            
            // Indicate this is GeoJSON content for CDN routing
            response.Headers["X-Content-Format"] = "geojson";
        }

        /// <summary>
        /// Configures response with cache for dynamic content
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <param name="maxAge">Cache duration in seconds</param>
        public static void ConfigureDynamicResponse(HttpResponse response, int maxAge = CacheDurations.ApiCache)
        {
            // Use unified cache duration for dynamic content
            response.Headers["Cache-Control"] = $"public, max-age={maxAge}, s-maxage={maxAge}";
            response.Headers["Vary"] = "Accept-Encoding";
            response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        /// <summary>
        /// Configures response to disable caching (for errors, auth endpoints)
        /// </summary>
        /// <param name="response">HTTP response</param>
        public static void ConfigureNoCacheResponse(HttpResponse response)
        {
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
        }

        /// <summary>
        /// Checks if the request includes conditional headers and content hasn't changed
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="etag">Current ETag</param>
        /// <param name="lastModified">Current last modified date</param>
        /// <returns>True if content hasn't changed (should return 304)</returns>
        public static bool IsNotModified(HttpRequest request, string etag, DateTime? lastModified = null)
        {
            // Check If-None-Match (ETag)
            if (request.Headers.ContainsKey("If-None-Match"))
            {
                var ifNoneMatch = request.Headers["If-None-Match"].ToString();
                if (ifNoneMatch == etag || ifNoneMatch == "*")
                {
                    return true;
                }
            }

            // Check If-Modified-Since
            if (lastModified.HasValue && request.Headers.ContainsKey("If-Modified-Since"))
            {
                if (DateTime.TryParse(request.Headers["If-Modified-Since"], out var ifModifiedSince))
                {
                    // Compare with second precision (HTTP dates don't include milliseconds)
                    var lastModifiedRounded = new DateTime(lastModified.Value.Year, lastModified.Value.Month, 
                        lastModified.Value.Day, lastModified.Value.Hour, lastModified.Value.Minute, 
                        lastModified.Value.Second, DateTimeKind.Utc);
                    
                    if (lastModifiedRounded <= ifModifiedSince)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a 304 Not Modified response with appropriate headers
        /// </summary>
        /// <param name="etag">ETag to include in response</param>
        /// <param name="lastModified">Last modified date to include in response</param>
        /// <returns>StatusCodeResult for 304 Not Modified</returns>
        public static IActionResult CreateNotModifiedResponse(string etag, DateTime? lastModified = null)
        {
            var result = new StatusCodeResult(304);
            return result;
        }

        /// <summary>
        /// Generates an ETag from content using SHA256 hash
        /// </summary>
        /// <param name="content">Content to hash</param>
        /// <returns>ETag string</returns>
        public static string GenerateETag(object content)
        {
            string jsonString;
            
            if (content is string str)
            {
                jsonString = str;
            }
            else
            {
                jsonString = JsonSerializer.Serialize(content, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false 
                });
            }

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonString));
            var hash = Convert.ToBase64String(hashBytes);
            
            return $"\"{hash}\"";
        }

        /// <summary>
        /// Configures CORS headers for CDN compatibility
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <param name="origin">Allowed origin (or * for all)</param>
        public static void ConfigureCorsHeaders(HttpResponse response, string origin = "*")
        {
            response.Headers["Access-Control-Allow-Origin"] = origin;
            response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, If-None-Match, If-Modified-Since";
            response.Headers["Access-Control-Expose-Headers"] = "ETag, Last-Modified, Cache-Control";
            response.Headers["Access-Control-Max-Age"] = "86400"; // 24 hours
        }
    }
}