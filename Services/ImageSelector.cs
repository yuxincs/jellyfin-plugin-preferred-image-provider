using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyfinPreferredImageProvider.Services
{
  /// <summary>
  /// Service for selecting the best image from a collection of images.
  /// </summary>
  public class ImageSelector
  {
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSelector"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ImageSelector(ILogger logger)
    {
      _logger = logger;
    }

    /// <summary>
    /// Selects the best image from a collection based on language priority, votes, and resolution.
    /// </summary>
    /// <param name="images">Collection of images to select from.</param>
    /// <param name="originalLanguage">The original language of the content.</param>
    /// <param name="itemName">Name of the item for logging purposes.</param>
    /// <param name="metadataLanguage">User's preferred metadata language.</param>
    /// <returns>The best image or null if no suitable image is found.</returns>
    public RemoteImageInfo SelectBestImage(IEnumerable<RemoteImageInfo> images, string originalLanguage, string itemName, string metadataLanguage = "en")
    {
      try
      {
        var imageList = images.ToList();
        if (!imageList.Any())
        {
          _logger.LogInformation("No images available for item '{ItemName}'", itemName);
          return null;
        }

        // Sort by: (1) language priority, (2) votes, (3) resolution.
        var bestImage = imageList
          .OrderByDescending(img => GetLanguagePriority(img, originalLanguage, metadataLanguage))
          .ThenByDescending(img => img.VoteCount ?? 0)
          .ThenByDescending(img => (long)((img.Width ?? 0) * (img.Height ?? 0)))
          .First();

        var languageType = GetLanguageType(bestImage, originalLanguage, metadataLanguage);
        _logger.LogInformation("Selected {LanguageType} {ImageType} image for item '{ItemName}': " +
                       "Language='{Language}', Votes={Votes}, Resolution={Width}x{Height}",
            languageType, bestImage.Type, itemName, bestImage.Language ?? "unknown",
            bestImage.VoteCount ?? 0, bestImage.Width ?? 0, bestImage.Height ?? 0);

        return bestImage;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error selecting best image for item '{ItemName}'", itemName);
        return null;
      }
    }

    private int GetLanguagePriority(RemoteImageInfo image, string originalLanguage, string metadataLanguage)
    {
      var language = image.Language?.ToLowerInvariant() ?? "";

      if (language == originalLanguage.ToLowerInvariant()) return 4; // Highest priority
      if (language == metadataLanguage.ToLowerInvariant()) return 3;
      if (language == "en" || language == "english" || string.IsNullOrEmpty(language)) return 2;
      return 1; // Other languages
    }

    private string GetLanguageType(RemoteImageInfo image, string originalLanguage, string metadataLanguage)
    {
      var language = image.Language?.ToLowerInvariant() ?? "";

      if (language == originalLanguage.ToLowerInvariant()) return "original language";
      if (language == metadataLanguage.ToLowerInvariant()) return "metadata language";
      if (language == "en" || language == "english" || string.IsNullOrEmpty(language)) return "English";
      return "other language";
    }
  }
}