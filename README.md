# Jellyfin Preferred Image Provider Plugin

**⚠️ This is a hacky stop-gap solution** to allow setting more preferences for the image fetcher while waiting for official Jellyfin image selection improvements for Jellyfin 10.9.0+.

This plugin provides images for Movies / TV Series / Seasons based on hardcoded priorities:

1. **Language**: Original language of the movie/series > preferred metadata language > English
2. **Resolution tier**: 4K+ > 2K > 1080p > 720p-
3. **Discounted community rating**: Within the same language priority and resolution tier, prefer the highest `rating × votes / (votes + 10)`
4. **Number of votes**: Images with more votes win when discounted ratings tie

The discounted rating blends community rating and vote count without calculating a baseline
or imposing a minimum vote cutoff. At 10 votes, an image retains half its rating; as votes
increase, its discounted rating approaches its community rating. For example, 9/10 from
20 votes scores 6.00, while 7/10 from 100 votes scores about 6.36.

Only Jellyfin `Score` ratings are used. Missing, negative, or non-finite ratings, missing or
nonpositive vote counts, and `Likes` ratings receive a discounted score of zero, falling back
to vote count when tied. Scores are compared as supplied by the providers, which assumes
comparable rating scales. No additional provider requests are needed.

Resolution tiers use the longest edge, treating portrait and landscape images consistently:
4K+ is at least 3840 pixels, 2K (1440p) is 2560–3839 pixels, 1080p is
1920–2559 pixels, and 720p- is anything below 1920 pixels (including unknown dimensions).
All resolutions within a tier are treated equally; exact pixel count does not break ties.

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
