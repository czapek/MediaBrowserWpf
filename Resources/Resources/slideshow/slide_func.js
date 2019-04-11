var BildNr = new Array(13);
var pos = new Array(0,3,8,4,11,2,7,12,10,6,9,1,5);
var webFolder = new Array("beachshow", "berlinshow", "flowershow", "ghostshow", "japanshow", "lifeshow", "russiashow", "templeshow", "thaishow");
var time = 120;
var noFade = false;

zeige = 0;

for (var i=0; i < BildNr.length; ++i){
        BildNr[pos[i]] = -1;
}



function resizePos(){
        w = Fensterweite();
        h = Fensterhoehe();



         if( w < 800) w = 855 - 20;
         if( h < 610) h = 690 - 4;
         document.images[0].width = w;
         document.images[0].height = h;

         if(w < 1000 && h < 750){
             document.images[pos[0]].style.visibility = "hidden";
             noFade = false;
         } else {
             document.images[pos[0]].style.visibility = "visible";
             noFade = true;
         }


        document.getElementsByTagName("div")[0].style.position = "absolute";

        if(navigator.appName == "Microsoft Internet Explorer"){
                document.getElementsByTagName("div")[0].style.top = Math.floor((h-530)/2)+"px";
                document.getElementsByTagName("div")[0].style.left = Math.floor((w-780)/2)+"px";
        } else {
                document.getElementsByTagName("div")[0].style.top = Math.floor((h-550)/2)+"px";
                document.getElementsByTagName("div")[0].style.left = Math.floor((w-800)/2)+"px";
        }


}

function Blaettern()
{


 time = time - (an_speed/1000);
 status = "Hit The Cat For Menue - Next Page in " + time + "s";
 window.defaultStatus = "Hit The Cat For Menue - Next Page in " + time + "s";

 if(time <= 0){
   my_folder_pos = my_folder_pos + 1;
   if(my_folder_pos == webFolder.length) my_folder_pos = 0;
   location.href="../" + webFolder[my_folder_pos] +  "/index.htm";
 }

 cnt++;
 pause_cnt++;


 if(pause_cnt>pause){
        if(cnt>12) cnt = 1;

        zeige=Math.floor(Math.random()*mainImage.length);
        while (BildNr[pos[cnt]] == zeige) {
                zeige=Math.floor(Math.random()*mainImage.length);
        }
        BildNr[pos[cnt]] = zeige;




        my_window = "javascript:maxbild(\""+mainImage[zeige] + "\",\"" + sizeX[zeige] + "\",\"" + sizeY[zeige] + "\",\""+mainImage[zeige]+"\")";


        document.getElementsByTagName("a")[pos[cnt]-1].href=my_window;
        if(navigator.appName == "Microsoft Internet Explorer" && !noFade) document.images[pos[cnt]].filters.blendTrans.Apply();
        document.images[pos[cnt]].src =  "thumbs/tn_" + mainImage[zeige];
        if(navigator.appName == "Microsoft Internet Explorer" && !noFade) document.images[pos[cnt]].filters.blendTrans.Play();
        document.images[pos[cnt]].border=my_border;

 }

 clearTimeout(tid1);
 tid1 = setTimeout('Blaettern()',an_speed);

}

function Fensterweite()
{
 if (window.innerWidth) return window.innerWidth;
 else if (document.body && document.body.offsetWidth) return document.body.offsetWidth-20;
 else return 0;
}

function Fensterhoehe()
{
 if (window.innerHeight) return window.innerHeight;
 else if (document.body && document.body.offsetHeight) return document.body.offsetHeight-4;
 else return 0;
}


document.body.style.backgroundImage = "url(thumbs/xxxx_back.jpg)"

Blaettern();

window.onresize=resizePos

resizePos();
