using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;
using JellyfinPreferredImageProvider.Services;
using MediaBrowser.Model.IO;

namespace JellyfinPreferredImageProvider.Providers
{
  /// <summary>
  /// Preferred image provider with language-aware selection.
  /// </summary>
  public class PreferredImageProvider : IRemoteImageProvider, IHasOrder
  {
    /// <summary>
    /// Logger instance.
    /// </summary>
    protected readonly ILogger _logger;
    
    /// <summary>
    /// HTTP client factory.
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;
    
    /// <summary>
    /// Language detector service.
    /// </summary>
    protected readonly LanguageDetector _languageDetector;
    
    /// <summary>
    /// Image selector service.
    /// </summary>
    protected readonly ImageSelector _imageSelector;
    
    /// <summary>
    /// Provider manager.
    /// </summary>
    protected readonly IProviderManager _providerManager;
    
    /// <summary>
    /// File system interface.
    /// </summary>
    protected readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferredImageProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="providerManager">Provider manager.</param>
    /// <param name="fileSystem">File system interface.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public PreferredImageProvider(ILogger<PreferredImageProvider> logger, IProviderManager providerManager, IFileSystem fileSystem, IHttpClientFactory httpClientFactory)
    {
      _logger = logger;
      _providerManager = providerManager;
      _fileSystem = fileSystem;
      _languageDetector = new LanguageDetector(_logger);
      _imageSelector = new ImageSelector(_logger);
      _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string Name => "Preferred Image Provider";

    /// <summary>
    /// Gets the provider order. This is a proxy to other providers so naturally we want to be the first provider.
    /// </summary>
    public int Order => 1;

    /// <summary>
    /// Determines if this provider supports the given item.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True if the item is supported.</returns>
    public bool Supports(BaseItem item)
    {
      return (item is Movie) || (item is Series);
    }

    /// <summary>
    /// Gets the supported image types for the given item.
    /// </summary>
    /// <param name="_">The item (unused).</param>
    /// <returns>Collection of supported image types.</returns>
    public IEnumerable<ImageType> GetSupportedImages(BaseItem _)
    {
      return new[] { ImageType.Primary, ImageType.Logo, ImageType.Thumb, ImageType.Backdrop };
    }

    /// <summary>
    /// Gets the HTTP response for the given image URL.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP response message.</returns>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
      return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
    }

    /// <summary>
    /// Gets images for the given ID.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Empty collection of images.</returns>
    public Task<IEnumerable<RemoteImageInfo>> GetImages(string id, CancellationToken cancellationToken)
    {
      return Task.FromResult(Enumerable.Empty<RemoteImageInfo>());
    }

    /// <summary>
    /// Gets the best images for the given item.
    /// </summary>
    /// <param name="item">The item to get images for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of selected best images.</returns>
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
      try
      {
        _logger.LogInformation("Processing images for item '{ItemName}'", item.Name);

        // Detect the original language
        var originalLanguage = _languageDetector.DetectOriginalLanguage(item);
        
        // Get metadata language (check various BaseItem properties)
        var metadataLanguage = GetMetadataLanguage(item);

        // Get all available images from other providers
        var allImages = await GetAllAvailableImages(item, cancellationToken);

        if (!allImages.Any())
        {
          _logger.LogInformation("No images available from other providers for item '{ItemName}'", item.Name);
          return Enumerable.Empty<RemoteImageInfo>();
        }

        // Select the best image for each supported image type
        var selectedImages = new List<RemoteImageInfo>();

        foreach (var imageType in GetSupportedImages(item))
        {
          var imagesOfType = allImages.Where(img => img.Type == imageType).ToList();
          if (!imagesOfType.Any())
          {
            _logger.LogInformation("No {ImageType} images available for item '{ItemName}'", imageType, item.Name);
            continue;
          }

          // Use ImageSelector to get the best image for this type
          var bestImage = _imageSelector.SelectBestImage(imagesOfType, originalLanguage, item.Name, metadataLanguage);
          if (bestImage != null)
          {
            selectedImages.Add(bestImage);
          }
        }

        return selectedImages;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting images for item '{ItemName}'", item.Name);
        return Enumerable.Empty<RemoteImageInfo>();
      }
    }

    private async Task<IEnumerable<RemoteImageInfo>> GetAllAvailableImages(BaseItem item, CancellationToken cancellationToken)
    {
      try
      {
        _logger.LogInformation("Getting all available images for item '{ItemName}' from other providers", item.Name);

        // Get all remote image providers except ourselves
        var refreshOptions = new ImageRefreshOptions(new DirectoryService(_fileSystem));
        var providers = _providerManager.GetImageProviders(item, refreshOptions)
            .OfType<IRemoteImageProvider>()
            .Where(p => !string.Equals(p.Name, this.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!providers.Any())
        {
          _logger.LogInformation("No other remote image providers found for item '{ItemName}'", item.Name);
          return Enumerable.Empty<RemoteImageInfo>();
        }

        _logger.LogInformation("Found {Count} remote image providers for item '{ItemName}': {Providers}",
            providers.Count, item.Name, string.Join(", ", providers.Select(p => p.Name)));

        // Get images from all providers concurrently
        var tasks = providers.Select(async provider =>
        {
          try
          {
            var images = await provider.GetImages(item, cancellationToken);
            var imageList = images.ToList();
            _logger.LogInformation("Provider '{ProviderName}' returned {Count} images for item '{ItemName}'",
                        provider.Name, imageList.Count, item.Name);
            return imageList;
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error getting images from provider '{ProviderName}' for item '{ItemName}'",
                        provider.Name, item.Name);
            return Enumerable.Empty<RemoteImageInfo>();
          }
        });

        var results = await Task.WhenAll(tasks);
        var allImages = results.SelectMany(r => r).ToList();

        _logger.LogInformation("Retrieved {Count} total images from all providers for item '{ItemName}'",
            allImages.Count, item.Name);

        return allImages;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in GetAllAvailableImages for item '{ItemName}'", item.Name);
        return Enumerable.Empty<RemoteImageInfo>();
      }
    }

    private string GetMetadataLanguage(BaseItem item)
    {
      // Try to get metadata language from various BaseItem properties
      // Check if item has PreferredMetadataLanguage property
      var metadataLanguage = item.GetPreferredMetadataLanguage();
      if (!string.IsNullOrEmpty(metadataLanguage))
      {
        _logger.LogInformation("Using metadata language '{Language}' for item '{ItemName}'", metadataLanguage, item.Name);
        return metadataLanguage;
      }

      // Fallback to English
      _logger.LogInformation("No metadata language found for item '{ItemName}', defaulting to 'en'", item.Name);
      return "en";
    }
  }
}