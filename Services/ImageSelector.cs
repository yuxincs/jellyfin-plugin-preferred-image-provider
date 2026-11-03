using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyfinPreferredImageProvider.Services
{
  /// <summary>
  /// Service for selecting the best image from a collection of images.
  /// </summary>
  public class ImageSelector
  {
    private const double RatingVoteWeight = 10;

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
    /// Selects the best image from a collection based on language priority, resolution tier, discounted community rating, and votes.
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

        // Sort by: (1) language, (2) resolution tier, (3) discounted rating, (4) votes.
        var bestImage = imageList
          .OrderByDescending(img => GetLanguagePriority(img, originalLanguage, metadataLanguage))
          .ThenByDescending(GetResolutionTier)
          .ThenByDescending(GetDiscountedRating)
          .ThenByDescending(img => img.VoteCount ?? 0)
          .First();

        var languageType = GetLanguageType(bestImage, originalLanguage, metadataLanguage);
        _logger.LogInformation("Selected {LanguageType} {ImageType} image for item '{ItemName}': " +
                       "Language='{Language}', Rating={Rating}, RatingType={RatingType}, " +
                       "DiscountedRating={DiscountedRating}, Votes={Votes}, Resolution={Width}x{Height}",
            languageType, bestImage.Type, itemName, bestImage.Language ?? "unknown",
            bestImage.CommunityRating, bestImage.RatingType, GetDiscountedRating(bestImage),
            bestImage.VoteCount ?? 0, bestImage.Width ?? 0, bestImage.Height ?? 0);

        return bestImage;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error selecting best image for item '{ItemName}'", itemName);
        return null;
      }
    }

    private static double GetDiscountedRating(RemoteImageInfo image)
    {
      var votes = image.VoteCount ?? 0;
      if (image.RatingType != RatingType.Score
          || votes <= 0
          || image.CommunityRating is not double rating
          || !double.IsFinite(rating)
          || rating < 0)
      {
        return 0;
      }

      // Discount scores with few votes without requiring a baseline or vote cutoff.
      return rating * (votes / (votes + RatingVoteWeight));
    }

    private static int GetResolutionTier(RemoteImageInfo image)
    {
      // Use the longest edge so portrait and landscape images share the same tiers.
      var longestEdge = Math.Max(image.Width ?? 0, image.Height ?? 0);

      if (longestEdge >= 3840) return 3; // 4K+
      if (longestEdge >= 2560) return 2; // 2K (1440p)
      if (longestEdge >= 1920) return 1; // 1080p
      return 0; // 720p-, including intermediate sizes and unknown dimensions
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