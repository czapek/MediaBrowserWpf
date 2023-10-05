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
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<div style=""width: 98vw; text-align:center;""><strong><a style=""margin: 1vw;"" href=""equirectangular.html"">Equi</a></strong><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style=""margin: 1vw;"" href=""original.html"">Original</a></div>
<div id=""viewer"" style=""width: 90vw; height: 85vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js"",
			""@photo-sphere-viewer/gyroscope-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/gyroscope-plugin/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';
    import { GyroscopePlugin } from '@photo-sphere-viewer/gyroscope-plugin';

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        plugins: [GyroscopePlugin],
        panorama: 'image.jpg',
    });
</script>";

        public const String Fisheye = @"<head>
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<div style=""width: 98vw; text-align:center;""><a style=""margin: 1vw;"" href=""equirectangular.html"">Equi</a><strong><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a></strong><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style=""margin: 1vw;"" href=""original.html"">Original</a></div>
<div id=""viewer"" style=""width: 90vw; height: 85vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js"",
			""@photo-sphere-viewer/gyroscope-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/gyroscope-plugin/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';
    import { GyroscopePlugin } from '@photo-sphere-viewer/gyroscope-plugin';

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        plugins: [GyroscopePlugin],
        panorama: 'image.jpg',
		defaultPitch: 0.6,
        defaultZoomLvl: 20,
        fisheye: true,
    });
</script>";

        public const String Littleplanet = @"<head>
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<div style=""width: 98vw; text-align:center;""><a style=""margin: 1vw;"" href=""equirectangular.html"">Equi</a><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><strong><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a></strong><a style=""margin: 1vw;"" href=""original.html"">Original</a></div>
<div id=""viewer"" style=""width: 90vw; height: 85vh; margin:1vw auto;""></div>
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
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
<div style=""width: 98vw; text-align:center;""><a style=""margin: 1vw;"" href=""equirectangular.html"">Equi</a><a style=""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style=""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><strong><a style=""margin: 1vw;"" href=""original.html"">Original</a></strong></div>
<img style=""display: block;; margin:1vw auto; width: 90vw;"" src=""image.jpg"">";
    }
}
