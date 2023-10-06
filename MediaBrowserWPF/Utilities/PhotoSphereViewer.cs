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
        public const String ParamVideoFisheye = "fisheye: 1.5, maxFov: 160, defaultZoomLvl: 80,";
        public const String ParamVideoEquirectangular = "fisheye: false,";
        public const String HeaderVideoEquirectangular = @"<div style=""width: 98vw; text-align:center;""><strong><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a></strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a></div>;";
        public const String HeaderVideoFisheye = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a><strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a></strong></div>;";


        public const String Video = @"<head>
    <title>5. Okt 2022, 16:45</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <link href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/settings-plugin@5/index.css"" rel=""stylesheet"">
</head>
{{header}}
<div id=""viewer"" style=""width: 90vw; height: 85vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js"",
			""@photo-sphere-viewer/gyroscope-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/gyroscope-plugin/index.module.js"",
			""@photo-sphere-viewer/autorotate-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/autorotate-plugin/index.module.js"",
			""@photo-sphere-viewer/video-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/video-plugin/index.module.js"",
			""@photo-sphere-viewer/equirectangular-video-adapter"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/equirectangular-video-adapter/index.module.js"",
            ""@photo-sphere-viewer/resolution-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/resolution-plugin/index.module.js"",
			""@photo-sphere-viewer/settings-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/settings-plugin/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';
    import { GyroscopePlugin } from '@photo-sphere-viewer/gyroscope-plugin';
	import { AutorotatePlugin } from '@photo-sphere-viewer/autorotate-plugin';
	import { VideoPlugin } from '@photo-sphere-viewer/video-plugin';
	import { EquirectangularVideoAdapter } from '@photo-sphere-viewer/equirectangular-video-adapter';
	import { ResolutionPlugin } from '@photo-sphere-viewer/resolution-plugin';
	import { SettingsPlugin } from '@photo-sphere-viewer/settings-plugin';

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        caption: '{{title}}',
		adapter: [EquirectangularVideoAdapter, {
            muted: false,
        }],
        plugins: [VideoPlugin, 
			SettingsPlugin,
		    GyroscopePlugin,  
		    [AutorotatePlugin, {
                autostartDelay: 10000,
                autorotateSpeed: '1rpm',
            }],
			[ResolutionPlugin, {
				defaultResolution: 'HD',
				resolutions: [
					{
						id: 'UHD',
						label: 'Ultra high',
						panorama: { source: 'video.mp4' },
					},
					{
						id: 'FHD',
						label: 'High',
						panorama: { source: 'video_4k.mp4' },
					},
					{
						id: 'HD',
						label: 'Standard',
						panorama: { source: 'video_2k.mp4' },
					},
				],
			}],
		],       
        {{param}}
    });
</script>";

        public const String ParamImageFisheye = "fisheye: 1.5, maxFov: 160, defaultZoomLvl: 80,";
        public const String ParamImageEquirectangular = "fisheye: false,";
        public const String HeaderImageEquirectangular = @"<div style=""width: 98vw; text-align:center;""><strong><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a></strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style = ""margin: 1vw;"" href=""original.html"">Original</a></div>";
        public const String HeaderImageFisheye = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a><strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a></strong><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style = ""margin: 1vw;"" href=""original.html"">Original</a></div>";
        public const String HeaderImageLittlePlanet = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><strong><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a></strong><a style = ""margin: 1vw;"" href=""original.html"">Original</a></div>";
        public const String HeaderImageOriginal = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><strong><a style = ""margin: 1vw;"" href=""original.html"">Original</a></strong></div>";

        public const String Image = @"<head>
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
{{header}}
<div id=""viewer"" style=""width: 90vw; height: 85vh; margin:1vw auto;""></div>
<script type=""importmap"">
    {
        ""imports"": {
            ""three"": ""https://cdn.jsdelivr.net/npm/three/build/three.module.js"",
            ""@photo-sphere-viewer/core"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.module.js"",
			""@photo-sphere-viewer/gyroscope-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/gyroscope-plugin/index.module.js"",
			""@photo-sphere-viewer/autorotate-plugin"": ""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/autorotate-plugin/index.module.js""
        }
    }
</script>
<script type=""module"">
    import { Viewer } from '@photo-sphere-viewer/core';
    import { GyroscopePlugin } from '@photo-sphere-viewer/gyroscope-plugin';
	import { AutorotatePlugin } from '@photo-sphere-viewer/autorotate-plugin';

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        caption: '{{title}}',
        plugins: [GyroscopePlugin,  
		  [AutorotatePlugin, {
            autostartDelay: 5000,
            autorotateSpeed: '0.3rpm',
        }]],
        panorama: 'image.jpg',
        {{param}}
    });
</script>";


        public const String Littleplanet = @"<head>
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
{{header}}
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
        caption: '{{title}}',
        panorama: 'image.jpg',
    });
</script>";

        public const String Original = @"<head>
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
{{header}}
<img style=""display: block;; margin:1vw auto; width: 90vw;"" src=""image.jpg"">";
    }
}
