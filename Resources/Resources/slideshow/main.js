cache = "leer";
flag = false;

function maxbild(pPage,a,b,txt){


time = time + 10;

my_size="";
sc="scrollbars=no,";
if(cache == pPage) flag=true;
div1 = ((screen.availWidth)-12)/((screen.availHeight)-30);
div2 = a/b*1;

if((b<100) && (a<100) && (flag==true)){
	ver = a/b;
	b=100;
	a=ver*b;
	my_size="height='"+b+"'";
	a=12+a*1;
	b=b*1+30;
	flag = false;
}else{
if((((screen.availWidth)-12)>=a && ((screen.availHeight)-30)>=b) && (flag==true)){
	if(div1<div2){
		ver = b/a;
		a=(screen.availWidth);
		b=ver*(a*1-12)+30;
		my_size="width='"+(a*1-12)+"'";
	}else{
		ver = a/b;
		b=(screen.availHeight);
		a=ver*(b*1-30)+12;
		my_size="height='"+(b*1-30)+"'";
	}
}else{
	if(((screen.availWidth)-12)<a && ((screen.availHeight)-30)<b){
		if(!flag){
			if(div1<div2){
				ver = b/a;
				a=(screen.availWidth)-12;
				my_size="width='"+a+"'";
				b = (ver*a)+30;
				a=12+a*1;
			}else{
				ver = a/b;
				b=(screen.availHeight)-30;
				my_size="height='"+b+"'";
				a = (ver*b)+12;
				b=b*1+30;
			}

		}else{
		b=(screen.availHeight)-30;
		a=(screen.availWidth)-12;
		sc="scrollbars=yes,";
		}
	}else{
	if(((screen.availHeight)-30) < b){
		if(!flag){
			ver = a/b;
			b=(screen.availHeight)-30;
			my_size="height='"+b+"'";
			a = (ver*b)+12;
			b=b*1+30;
		}else{
			sc="scrollbars=yes,";
			a=16+a*1;
			b=(screen.availHeight)-30;
		}
	}else{
		if(((screen.availWidth)-12) < a){
			if(!flag){
				ver = b/a;
				a=(screen.availWidth)-12;
				my_size="width='"+a+"'";
				b = (ver*a)+30;
				a=12+a*1;
			}else{
				sc="scrollbars=yes,";
				b=b*1+15;
				a=(screen.availWidth)-12;
			}
		}
	}
}
if(((screen.availWidth)-12)>=a && ((screen.availHeight)-30)>=b){
	a=a*1+12;
	b=b*1+30;
}
}
cache = pPage;
if(flag){
	flag = false;
	cache = "";
}
}




popUpWin = window.open('','popWin','resizable=yes,'+sc+'width='+a+',height='+b+',screenX=0,screenY=0');
popUpWin.moveTo(0,0);
if(navigator.appName == "Microsoft Internet Explorer"){
        popUpWin.resizeTo(a,b);
} else {
        popUpWin.resizeTo(a-2,b+9);
}
popUpWin.focus();
figDoc= popUpWin.document;
zhtm= '<HTML><HEAD><TITLE>' + pPage + '</TITLE>';
zhtm += '</HEAD>';
zhtm += '<BODY bgcolor="#FFFFFF" topmargin="0" leftmargin="0">';
zhtm += '<div align="center">';
zhtm += '<table border="0" cellspacing="0" width="100%" height="100%">';
zhtm += '<tr>';
zhtm += '<td width="0" valign="middle" align="center">';
zhtm += '<a href="javascript:window.close()"><img src="' + pPage + '" border="0" ' + my_size + ' alt="'+txt+'"></a>';
zhtm += '</td></tr>';
zhtm += '</table>';
zhtm += '</div>';
zhtm += '</BODY></HTML>';
window.popUpWin.document.write(zhtm);
window.popUpWin.document.close();
}


function quicktime(pPage,a,b,txt){

popUpWin = window.open('','popWin','resizable=no,scrollbars=no,width=320,height=240,screenX=0,screenY=0');
popUpWin.focus();
figDoc= popUpWin.document;
zhtm= '<HTML><HEAD><TITLE>' + pPage + '</TITLE>';
zhtm += '</HEAD>';
zhtm += '<BODY bgcolor="#FFFFFF" topmargin="0" leftmargin="0">';
zhtm += '<div align="center">';
zhtm += '<table border="0" cellspacing="0" width="100%" height="100%">';
zhtm += '<tr>';
zhtm += '<td width="0" valign="middle" align="center">';
zhtm += '<embed width="320" src="'+pPage+'" height="240" controller="false" href="javascript:window.close()" border="0" target="Myself" autoplay="true">';
zhtm += '</td></tr>';
zhtm += '</table>';
zhtm += '</div>';
zhtm += '</BODY></HTML>';
window.popUpWin.document.write(zhtm);
window.popUpWin.document.close();

}


