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
        public const String HeaderVideoEquirectangular = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""../{{prev}}/equirectangular.html"">Prev</a><strong><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a></strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style = ""margin: 1vw;"" href=""../{{next}}/equirectangular.html"">Next</a></div>;";
        public const String HeaderVideoFisheye = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""../{{prev}}/fisheye.html"">Prev</a><a style = ""margin: 1vw;"" href=""../{{next}}/fisheye.html"">Next</a></div>;";


        public const String Video = @"<head>
    <title>{{title}}</title>
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

	const prev = ""{{prev}}"";
	const next = ""{{next}}"";

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
        touchmoveTwoFingers: false,
        {{param}}
    });
</script>";

        public const String ParamImageFisheye = "fisheye: 1.5, maxFov: 160, defaultZoomLvl: 80,";
        public const String ParamImageEquirectangular = "fisheye: false,";
        public const String HeaderImageEquirectangular = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""../{{prev}}/equirectangular.html"">Prev</a><strong><a style = ""margin: 1vw;"" href=""equirectangular.html"">Equi</a></strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style = ""margin: 1vw;"" href=""original.html"">Original</a><a style = ""margin: 1vw;"" href=""../{{next}}/equirectangular.html"">Next</a></div>";
        public const String HeaderImageFisheye = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""../{{prev}}/fisheye.html"">Prev</a><strong><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a></strong><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><a style = ""margin: 1vw;"" href=""original.html"">Original</a><a style = ""margin: 1vw;"" href=""../{{next}}/fisheye.html"">Next</a></div>";
        public const String HeaderImageLittlePlanet = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""../{{prev}}/littleplanet.html"">Prev</a><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><strong><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a></strong><a style = ""margin: 1vw;"" href=""original.html"">Original</a><a style = ""margin: 1vw;"" href=""../{{next}}/littleplanet.html"">Next</a></div>";
        public const String HeaderImageOriginal = @"<div style=""width: 98vw; text-align:center;""><a style = ""margin: 1vw;"" href=""../{{prev}}/original.html"">Prev</a><a style = ""margin: 1vw;"" href=""fisheye.html"">Fisheye</a><a style = ""margin: 1vw;"" href=""littleplanet.html"">Littleplanet</a><strong><a style = ""margin: 1vw;"" href=""original.html"">Original</a></strong><a style = ""margin: 1vw;"" href=""../{{next}}/original.html"">Next</a></div>";

        public const String Image = @"<head>
    <title>{{title}}</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/@photo-sphere-viewer/core/index.min.css"" />
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"" rel=""stylesheet"">
</head>
{{header}}
<div id=""viewer"" style=""width: 90vw; height: 82vh; margin:1vw auto;""></div>
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

	const prev = ""{{prev}}"";
	const next = ""{{next}}"";

    const viewer = new Viewer({
        container: document.querySelector('#viewer'),
        caption: '{{title}}',
        plugins: [GyroscopePlugin,  
		  [AutorotatePlugin, {
            autostartDelay: 10000,
            autorotateSpeed: '0.3rpm',
        }]],
        panorama: 'image.jpg',
		defaultYaw: '{{defaultYaw}}deg',
		defaultPitch: '{{defaultPitch}}deg',
        touchmoveTwoFingers: false,
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
<div id=""viewer"" style=""width: 90vw; height: 82vh; margin:1vw auto;""></div>
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
	const prev = ""{{prev}}"";
	const next = ""{{next}}"";

    const viewer = new Viewer({
	    adapter: LittlePlanetAdapter,
        container: document.querySelector('#viewer'),
        caption: '{{title}}',
        panorama: 'image.jpg',
        touchmoveTwoFingers: false,
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

		public const string ImageVr = @"<!DOCTYPE html>
<html lang=""en"">
	<head>
		<title>{{title}}</title>
		<meta charset=""utf-8"">
		<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=no"">
		<link type=""text/css"" rel=""stylesheet"" href=""main.css"">
	</head>
	<body style=""margin: 0;"">
		<div style=""width: 98vw; text-align: center"">
		  <a style=""margin: 1vw"" href=""../{{prev}}/vr.html"">Prev</a><a style=""margin: 1vw"" href=""../{{next}}/vr.html"">Next</a>
		</div>
		<div id=""container""></div>
		
		<script type=""importmap"">
			{
				""imports"": {
					""three"": ""../treejs/three.module.js"",
					""three/addons/"": ""../treejs/jsm/""
				}
			}
		</script>

		<script type=""module"">

			import * as THREE from 'three';
			import { VRButton } from 'three/addons/webxr/VRButton.js';

			let camera, scene, renderer, sphere, clock;
			const prev = ""{{prev}}"";
			const next = ""{{next}}"";

			init();
			animate();

			function init() {

				const container = document.getElementById( 'container' );

				clock = new THREE.Clock();

				scene = new THREE.Scene();
				scene.background = new THREE.Color( 0x101010 );

				const light = new THREE.AmbientLight( 0xffffff, 3 );
				scene.add( light );

				camera = new THREE.PerspectiveCamera( 70, window.innerWidth / window.innerHeight, 1, 2000 );
				scene.add( camera );

				const panoSphereGeo = new THREE.SphereGeometry( 500, 100, 60 );		
				const panoSphereMat = new THREE.MeshStandardMaterial( {
					side: THREE.BackSide
				} );

				sphere = new THREE.Mesh( panoSphereGeo, panoSphereMat );
			    const manager = new THREE.LoadingManager();
				const loader = new THREE.TextureLoader( manager );

				loader.load( 'image.jpg', function ( texture ) {
					texture.colorSpace = THREE.SRGBColorSpace;
					texture.minFilter = THREE.NearestFilter;
					texture.generateMipmaps = false;
					texture.wrapS = THREE.RepeatWrapping;
					texture.repeat.x = - 1;
					sphere.material.map = texture;
				} );

	     		manager.onLoad = function () {
					scene.add( sphere );
				};

				renderer = new THREE.WebGLRenderer();
				renderer.setPixelRatio( window.devicePixelRatio );
				renderer.setSize( window.innerWidth, window.innerHeight );
				renderer.xr.enabled = true;
				renderer.xr.setReferenceSpaceType( 'local' );
				container.appendChild( renderer.domElement );

				document.body.appendChild( VRButton.createButton( renderer ) );

				window.addEventListener( 'resize', onWindowResize );
			}

			function onWindowResize() {
				camera.aspect = window.innerWidth / window.innerHeight;
				camera.updateProjectionMatrix();
				renderer.setSize( window.innerWidth, window.innerHeight );
			}

			function animate() {
				renderer.setAnimationLoop( render );
			}

			function render() {
				if ( renderer.xr.isPresenting === false ) {
					const time = clock.getElapsedTime();
					sphere.rotation.y += 0.0005;
					sphere.position.x = Math.sin( time ) * 0.2;
					sphere.position.z = Math.cos( time ) * 0.2;
				}

				renderer.render( scene, camera );
			}

		</script>
	</body>
</html>";

        public const string VideoVr = @"<!DOCTYPE html>
<html lang=""en"">
	<head>
		 <title>{{title}}</title>
		<meta charset=""utf-8"">
		<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=no"">
		<link type=""text/css"" rel=""stylesheet"" href=""../treejs/main.css"">
	</head>
	<body style=""margin: 0;"">
		<div style=""width: 98vw; text-align: center"">
		  <a style=""margin: 1vw"" href=""../{{prev}}/vr.html"">Prev</a><a style=""margin: 1vw"" href=""../{{next}}/vr.html"">Next</a>
		</div>
		<div id=""container""></div>

		<video id=""video"" loop muted crossOrigin=""anonymous"" playsinline style=""display:none"">
			<source src=""video.mp4"">
		</video>

		<script type=""importmap"">
			{
				""imports"": {
					""three"": ""../treejs/three.module.js"",
					""three/addons/"": ""../treejs/jsm/""
				}
			}
		</script>

		<script type=""module"">

			import * as THREE from 'three';
			import { VRButton } from 'three/addons/webxr/VRButton.js';

			let camera, scene, renderer;
			const prev = ""{{prev}}"";
			const next = ""{{next}}"";

			init();
			animate();

			function init() {

				const container = document.getElementById( 'container' );
				container.addEventListener( 'click', function () {
					video.muted = false;
					video.isPlaying = true;
					video.play();
				    
				} );

				camera = new THREE.PerspectiveCamera( 70, window.innerWidth / window.innerHeight, 1, 2000 );
				camera.layers.enable( 1 ); 
		

				const video = document.getElementById( 'video' );
				video.muted = false;
				video.isPlaying = true;
				video.play();
				

				const texture = new THREE.VideoTexture( video );
				texture.colorSpace = THREE.SRGBColorSpace;

				scene = new THREE.Scene();
				scene.background = new THREE.Color( 0x101010 );

				const geometry1 = new THREE.SphereGeometry( 500, 100, 60 );
				geometry1.scale( - 1, 1, 1 );


				const material1 = new THREE.MeshBasicMaterial( { map: texture } );
				const mesh1 = new THREE.Mesh( geometry1, material1 );
				scene.add( mesh1 );
	
				renderer = new THREE.WebGLRenderer();
				renderer.setPixelRatio( window.devicePixelRatio );
				renderer.setSize( window.innerWidth, window.innerHeight );
				renderer.xr.enabled = true;
				renderer.xr.setReferenceSpaceType( 'local' );
				container.appendChild( renderer.domElement );

				document.body.appendChild( VRButton.createButton( renderer ) );

				//

				window.addEventListener( 'resize', onWindowResize );

			}

			function onWindowResize() {

				camera.aspect = window.innerWidth / window.innerHeight;
				camera.updateProjectionMatrix();

				renderer.setSize( window.innerWidth, window.innerHeight );

			}

			function animate() {

				renderer.setAnimationLoop( render );

			}

			function render() {

				renderer.render( scene, camera );

			}

		</script>
	</body>
</html>";
    }
}
