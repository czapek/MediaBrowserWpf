using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowserWPF.Utilities
{
    public static class PhotoSphereViewer
    {
        public const String Equirectangular = @"<head>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
</head>
<div style=""width: 98vw; text-align:center;""><strong><a style=""margin: 1vw;"" href=""equirectangular.html"">Equirectangular</a></strong><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style=""margin: 1vw;"" href=""original.html"">Original</a></div>
<div id=""viewer"" style=""width: 90vw; height: 90vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        panorama: 'image.jpg',
    });
</script>";

        public const String Fisheye = @"<head>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
</head>
<div style=""width: 98vw; text-align:center;""><a style=""margin: 1vw;"" href=""equirectangular.html"">Equirectangular</a><strong><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a></strong><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style=""margin: 1vw;"" href=""original.html"">Original</a></div>
<div id=""viewer"" style=""width: 90vw; height: 90vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        panorama: 'image.jpg',
		defaultPitch: 0.6,
        defaultZoomLvl: 20,
        fisheye: true,
    });
</script>";

        public const String Littleplanet = @"<head>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
</head>
<div style=""width: 98vw; text-align:center;""><a style=""margin: 1vw;"" href=""equirectangular.html"">Equirectangular</a><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><strong><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a></strong><a style=""margin: 1vw;"" href=""original.html"">Original</a></div>
<div id=""viewer"" style=""width: 90vw; height: 90vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js"",
			""@photo-sphere-viewer/little-planet-adapter"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/little-planet-adapter/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';
    import { LittlePlanetAdapter } from '@photo-sphere-viewer/little-planet-adapter';

    const viewer = new Viewer({
	    adapter: LittlePlanetAdapter,
        container: document.querySelector('#viewer'),
        panorama: 'image.jpg',
    });
</script>";

        public const String Original = @"<head>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
</head>
<div style=""width: 98vw; text-align:center;""><a style=""margin: 1vw;"" href=""equirectangular.html"">Equirectangular</a><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><strong><a style=""margin: 1vw;"" href=""original.html"">Original</a></strong></div>
<img style=""display: block;; margin:1vw auto; width: 90vw;"" src=""image.jpg"">";
    }
}
