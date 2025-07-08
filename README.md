# Jellyfin Preferred Image Provider Plugin

**⚠️ This is a hacky stop-gap solution** to allow setting more preferences for the image fetcher while waiting for official Jellyfin image selection improvements for Jellyfin 10.9.0+.

This plugin provides images for Movies / TV Series based on hardcoded priorities:

1. **Language**: Original language of the movie/series > preferred metadata language > English
2. **Number of votes**: Images with more votes are preferred
3. **Resolution**: Higher resolution images are prioritized

The images are obtained by the existing configured image providers, this provider simply does the
final selection and provide the best image with highest order (such that it is always picked before
other providers).

This plugin can be further generalized with better configurations if there are enough interests (raise an issue!) and
Jellyfin does not provider more configurations to the image fetcher settings.

## Installation

1. **Build the plugin** (requires dotnet 8.0):

   ```bash
   cd jellyfin-plygin-preferred-image-provider
   dotnet build -c Release
   ```

2. **Copy to Jellyfin**:

   ```bash
   # Copy the DLL to your Jellyfin plugins directory
   cp bin/Release/net8.0/JellyfinPreferredImageProvider.dll /path/to/jellyfin/plugins/JellyfinPreferredImageProvider_0.0.1.0
   ```

3. **Restart Jellyfin server**

4. Enable "Preferred Image Provider" for the libraries you would like it to be enabled (note that this provider must be set as the highest order to override the other providers).

