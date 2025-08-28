using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyfinPreferredImageProvider.Services
{
  /// <summary>
  /// Service for detecting the original language of media items.
  /// </summary>
  public class LanguageDetector
  {
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageDetector"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public LanguageDetector(ILogger logger)
    {
      _logger = logger;
    }

    /// <summary>
    /// Detects the original language of a media item based on production locations and other metadata.
    /// </summary>
    /// <param name="item">The media item to analyze.</param>
    /// <returns>The detected language code (e.g., "en", "ja", "zh").</returns>
    public string DetectOriginalLanguage(BaseItem item)
    {
      try
      {
        _logger.LogInformation("Detecting language for item '{ItemName}' (Type: {ItemType})", item.Name, item.GetType().Name);

        // Special handling for Season - get language from parent Series
        if (item is Season season && season.Series != null)
        {
          _logger.LogInformation("Season '{SeasonName}' found, using parent series '{SeriesName}' for language detection", 
            season.Name, season.Series.Name);
          return DetectOriginalLanguage(season.Series);
        }

        // Try various language detection methods
        string detectedLanguage = null;

        // Method 1: Check for explicit language properties
        detectedLanguage = TryGetLanguageFromMetadata(item);
        if (!string.IsNullOrEmpty(detectedLanguage))
        {
          return detectedLanguage;
        }

        // Method 2: Check production locations
        detectedLanguage = TryGetLanguageFromProductionLocations(item);
        if (!string.IsNullOrEmpty(detectedLanguage))
        {
          return detectedLanguage;
        }

        // Method 3: Check original title for CJK language hints
        detectedLanguage = TryGetLanguageFromTitle(item);
        if (!string.IsNullOrEmpty(detectedLanguage))
        {
          return detectedLanguage;
        }

        // Default fallback
        _logger.LogInformation("Could not detect original language for item '{ItemName}', using default 'en'", item.Name);
        return "en";
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error detecting language for item '{ItemName}'", item.Name);
        return "en";
      }
    }

    private string TryGetLanguageFromMetadata(BaseItem item)
    {
      // Priority order: Studios > Tags > Genres
      
      // Check networks for regional hints
      if (item.Studios != null)
      {
        foreach (var studio in item.Studios)
        {
          var languageFromStudio = MapStudioToLanguage(studio);
          if (!string.IsNullOrEmpty(languageFromStudio))
          {
            _logger.LogInformation("Mapped studio '{Studio}' to language '{Language}' for item '{ItemName}'", 
              studio, languageFromStudio, item.Name);
            return languageFromStudio;
          }
        }
      }

      // Check for explicit language metadata in tags
      if (item.Tags != null)
      {
        foreach (var tag in item.Tags)
        {
          var normalizedTag = tag.ToLowerInvariant();
          if (normalizedTag.Contains("japanese") || normalizedTag.Contains("japan")) return "ja";
          if (normalizedTag.Contains("korean") || normalizedTag.Contains("korea")) return "ko";
          if (normalizedTag.Contains("chinese") || normalizedTag.Contains("china") || normalizedTag.Contains("mandarin")) return "zh";
          if (normalizedTag.Contains("spanish") || normalizedTag.Contains("spain")) return "es";
          if (normalizedTag.Contains("french") || normalizedTag.Contains("france")) return "fr";
          if (normalizedTag.Contains("german") || normalizedTag.Contains("germany")) return "de";
          if (normalizedTag.Contains("italian") || normalizedTag.Contains("italy")) return "it";
        }
      }

      // Check genres for language hints
      if (item.Genres != null)
      {
        foreach (var genre in item.Genres)
        {
          var normalizedGenre = genre.ToLowerInvariant();
          if (normalizedGenre.Contains("anime") || normalizedGenre.Contains("j-drama")) return "ja";
          if (normalizedGenre.Contains("k-drama") || normalizedGenre.Contains("korean")) return "ko";
          if (normalizedGenre.Contains("c-drama") || normalizedGenre.Contains("chinese")) return "zh";
        }
      }

      return null;
    }


    private string MapStudioToLanguage(string studio)
    {
      var normalized = studio.ToLowerInvariant();

      // Japanese studios/networks
      if (normalized.Contains("nhk") || normalized.Contains("fuji") || normalized.Contains("toei") || 
          normalized.Contains("mappa") || normalized.Contains("kyoto animation") || normalized.Contains("studio ghibli") ||
          normalized.Contains("madhouse") || normalized.Contains("bones") || normalized.Contains("shaft")) 
        return "ja";

      // Korean studios/networks  
      if (normalized.Contains("sbs") || normalized.Contains("kbs") || normalized.Contains("mbc") ||
          normalized.Contains("tvn") || normalized.Contains("jtbc") || normalized.Contains("ocn"))
        return "ko";

      // Chinese studios/networks
      if (normalized.Contains("cctv") || normalized.Contains("youku") || normalized.Contains("iqiyi") ||
          normalized.Contains("tencent") || normalized.Contains("bilibili"))
        return "zh";

      return null;
    }

    private string TryGetLanguageFromProductionLocations(BaseItem item)
    {
      if (item.ProductionLocations == null || !item.ProductionLocations.Any())
      {
        _logger.LogInformation("No production locations found for item '{ItemName}'", item.Name);
        return null;
      }

      _logger.LogInformation("Found production locations for item '{ItemName}': {Locations}",
        item.Name, string.Join(", ", item.ProductionLocations));

      foreach (var location in item.ProductionLocations)
      {
        var normalizedLocation = location.ToLowerInvariant();

        if (normalizedLocation.Contains("japan")) return "ja";
        if (normalizedLocation.Contains("south korea") || normalizedLocation.Contains("korea")) return "ko";
        if (normalizedLocation.Contains("china") || normalizedLocation.Contains("hong kong") || normalizedLocation.Contains("taiwan")) return "zh";
        if (normalizedLocation.Contains("united states") || normalizedLocation.Contains("usa") || normalizedLocation.Contains("america")) return "en";
        if (normalizedLocation.Contains("united kingdom") || normalizedLocation.Contains("uk") || normalizedLocation.Contains("britain")) return "en";
        if (normalizedLocation.Contains("spain")) return "es";
        if (normalizedLocation.Contains("france")) return "fr";
        if (normalizedLocation.Contains("germany")) return "de";
        if (normalizedLocation.Contains("italy")) return "it";
      }

      return null;
    }

    private string TryGetLanguageFromTitle(BaseItem item)
    {
      if (string.IsNullOrEmpty(item.OriginalTitle))
      {
        return null;
      }

      // Check for specific CJK language scripts in priority order: Korean > Japanese > Chinese
      if (item.OriginalTitle.Any(c =>
          (c >= 0xAC00 && c <= 0xD7AF) ||  // Hangul Syllables
          (c >= 0x1100 && c <= 0x11FF) ||  // Hangul Jamo
          (c >= 0x3130 && c <= 0x318F) ||  // Hangul Compatibility Jamo
          (c >= 0xA960 && c <= 0xA97F) ||  // Hangul Jamo Extended-A
          (c >= 0xD7B0 && c <= 0xD7FF)))   // Hangul Jamo Extended-B
      {
        _logger.LogInformation("Detected Korean characters in original title for item '{ItemName}'", item.Name);
        return "ko";
      }

      if (item.OriginalTitle.Any(c =>
          (c >= 0x3040 && c <= 0x309F) ||  // Hiragana
          (c >= 0x30A0 && c <= 0x30FF) ||  // Katakana
          (c >= 0x31F0 && c <= 0x31FF)))   // Katakana Phonetic Extensions
      {
        _logger.LogInformation("Detected Japanese characters in original title for item '{ItemName}'", item.Name);
        return "ja";
      }

      if (item.OriginalTitle.Any(c =>
          (c >= 0x4E00 && c <= 0x9FFF) ||  // CJK Unified Ideographs
          (c >= 0x3400 && c <= 0x4DBF) ||  // CJK Extension A
          (c >= 0xF900 && c <= 0xFAFF) ||  // CJK Compatibility Ideographs
          (c >= 0x20000 && c <= 0x2A6DF) || // CJK Extension B
          (c >= 0x2A700 && c <= 0x2B73F) || // CJK Extension C
          (c >= 0x2B740 && c <= 0x2B81F) || // CJK Extension D
          (c >= 0x2B820 && c <= 0x2CEAF)))  // CJK Extension E
      {
        _logger.LogInformation("Detected Chinese characters in original title for item '{ItemName}'", item.Name);
        return "zh";
      }

      return null;
    }
  }
}