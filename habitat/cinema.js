'use strict';
// No duplicate bird, action commands, or fabricated reactions. Only the visible
// scenery is sampled into the existing authenticated local habitat retina.
(() => {
  const $ = id => document.getElementById(id);
  const canvas = $('world'), ctx = canvas.getContext('2d');
  const eye = $('retina').getContext('2d', {willReadFrequently:true});
  const duration = 44;
  const chapters = [
    [0, 'I', 'A creature feature', 'Small bird.<br>Big universe.', 'Something is stirring in the clearing.'],
    [6, 'II', 'A light in the dark', 'Oh.<br>What’s that?', 'Curiosity has entered the chat.'],
    [16, 'III', 'Finders, keepers', 'Shiny.<br>Therefore mine.', 'Ownership is a state of mind.'],
    [24, 'IV', 'A slight misunderstanding', 'Excuse<br>you.', 'Personal space. Look it up.'],
    [30, 'V', 'A moment of wonder', 'The world<br>is rather big.', 'He’ll deny being impressed.'],
    [36, 'VI', 'Goodnight, little menace', 'Same time<br>tomorrow?', 'One small bird. An unreasonable amount of personality.']
  ];
  let time=0, playing=false, last=0, painted=0, current=-1, countdownUntil=0;
  let busy=false, live=false, statusTime=0, hasFrame=false, drawnTime=-1, drawnWidth=0;
  let cinemaToken='', walls=false, selecting=false, layout='', changingWalls=false;
  const local = location.protocol==='http:' && location.hostname==='127.0.0.1';
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const clamp = (x,a=0,b=1) => Math.max(a,Math.min(b,x));
  const smooth = x => { x=clamp(x); return x*x*(3-2*x); };
  const noise = n => { const v=Math.sin(n*127.1+311.7)*43758.5453; return v-Math.floor(v); };
  function circle(x,y,r,color){ctx.fillStyle=color;ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.fill();}
  function glow(x,y,r,color){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,color);g.addColorStop(1,'transparent');ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2);}
  function tree(x,base,height,width,color,wind){
    ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(x-width*.15,base);ctx.lineTo(x+width*.12,base);
    ctx.lineTo(x+wind*.4,base-height);ctx.closePath();ctx.fill();
    for(let j=0;j<9;j++){
      const y=base-height*.16-j*height*.078, span=width*(1-j*.083);
      ctx.beginPath();ctx.moveTo(x-span,y+height*.045);ctx.quadraticCurveTo(x-span*.35,y-height*.06,x+wind, y-height*.17);
      ctx.quadraticCurveTo(x+span*.4,y-height*.055,x+span,y+height*.045);ctx.lineTo(x,y);ctx.closePath();ctx.fill();
    }
  }
  function fern(x,y,scale,side,t){
    ctx.save();ctx.translate(x,y);ctx.scale(scale*side,scale);ctx.rotate(Math.sin(t*.3+x)*.018);
    ctx.strokeStyle='#263f42';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(0,0);ctx.quadraticCurveTo(10,-70,60,-140);ctx.stroke();
    for(let j=0;j<9;j++){const yy=-j*14,xx=j*j*.7;for(const s of[-1,1]){ctx.fillStyle=j%2?'#233a3d':'#304748';ctx.beginPath();ctx.moveTo(xx,yy);ctx.quadraticCurveTo(xx+s*33,yy+5,xx+s*29,yy-18);ctx.quadraticCurveTo(xx+s*8,yy-12,xx,yy);ctx.fill();}}
    ctx.restore();
  }
  function moth(x,y,t,scale=1){
    ctx.save();ctx.translate(x,y);ctx.scale(scale,scale);
    glow(0,0,85,'rgba(238,204,130,.23)');glow(0,0,30,'rgba(255,232,171,.4)');
    const flutter=.65+.3*Math.sin(t*22);
    ctx.fillStyle='#e9e1c8';ctx.beginPath();ctx.ellipse(-17,-3,23,12*flutter,-.45,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.ellipse(17,-3,23,12*flutter,.45,0,Math.PI*2);ctx.fill();
    circle(0,0,19,'#fff5cd');circle(0,-2,7,'#fffbe6');ctx.restore();
  }
  function draw(t){
    if(t===drawnTime&&canvas.width===drawnWidth)return;
    drawnTime=t;drawnWidth=canvas.width;
    const w=canvas.width,h=canvas.height,wide=w>h, unit=Math.min(w,h)/720;
    const wonder=smooth((t-30)/4)*(1-smooth((t-36)/7));
    const bg=ctx.createLinearGradient(0,0,w*.25,h);bg.addColorStop(0,'#111a30');bg.addColorStop(.5,wonder>.3?'#243549':'#1a2a37');bg.addColorStop(1,'#101e24');ctx.fillStyle=bg;ctx.fillRect(0,0,w,h);
    // All environmental lights remain below the retina's bright-object threshold.
    glow(w*.68,h*.29,w*.63,'rgba(76,106,125,.17)');
    for(let i=0;i<100;i++){
      const x=noise(i+4)*w,y=noise(i+144)*h*.66;
      const alpha=.18+noise(i+24)*.23+Math.sin(t*.5+i)*.08;
      circle(x,y,(.5+noise(i)*1.1)*unit,`rgba(162,178,190,${alpha})`);
    }
    const moonX=w*.73,moonY=h*(wide?.27:.37),moonR=43*unit;
    glow(moonX,moonY,moonR*4,'rgba(158,175,179,.10)');circle(moonX,moonY,moonR,'#99aeb4');circle(moonX+moonR*.38,moonY-moonR*.17,moonR*.91,'#1b2b3b');
    for(let layer=0;layer<3;layer++){
      const base=h*(.77+layer*.085),col=['#1a2e38','#152a30','#102128'][layer];
      for(let i=0;i<11;i++){
        const x=(i/10-.07)*w+Math.sin(i*5+layer)*w*.04;
        tree(x,base,h*(.18+noise(i+layer*16)*.25),w*(wide?.09:.16),col,Math.sin(t*.3+i)*4*unit);
      }
    }
    // Mist is deliberately soft and dark, not a flashing full-field stimulus.
    for(let i=0;i<5;i++){
      const y=h*(.61+i*.075),x=w*(.5+Math.sin(t*.07+i)*.22);
      ctx.save();ctx.translate(x,y);ctx.scale(1,.12);glow(0,0,w*.75,'rgba(92,122,130,.13)');ctx.restore();
    }
    const py=h*.76;
    ctx.lineCap='round';ctx.strokeStyle='#151a20';ctx.lineWidth=24*unit;ctx.beginPath();ctx.moveTo(-40,py+h*.06);ctx.bezierCurveTo(w*.16,py-h*.01,w*.62,py+h*.012,w+40,py+h*.05);ctx.stroke();
    ctx.strokeStyle='#5b5950';ctx.lineWidth=3*unit;ctx.beginPath();ctx.moveTo(w*.17,py+.001*h);ctx.quadraticCurveTo(w*.45,py-.004*h,w*.74,py+.022*h);ctx.stroke();
    ctx.strokeStyle='#272d2c';ctx.lineWidth=8*unit;ctx.beginPath();ctx.moveTo(w*.79,py+.03*h);ctx.quadraticCurveTo(w*.86,py-.03*h,w*.96,py-.065*h);ctx.stroke();
    fern(-15,h*.98,2.2*unit,1,t);fern(w+15,h,2.8*unit,-1,t);fern(w*.12,h*1.05,1.6*unit,1,t);
    for(let i=0;i<24;i++){
      const x=(noise(i+66)*w+Math.sin(t*.11+i)*18*unit),y=(noise(i+95)*h+t*(2+noise(i)*4)*unit)%h;
      circle(x,y,(.7+noise(i+4))*unit,'rgba(176,151,104,.25)');
    }
    // Shade the forest before drawing luminous subjects, so a fly-by remains
    // visible to the retina even near the edge of a landscape recording.
    const vignette=ctx.createRadialGradient(w*.5,h*.5,w*.13,w*.5,h*.5,h*.8);vignette.addColorStop(0,'transparent');vignette.addColorStop(1,'rgba(3,7,13,.65)');ctx.fillStyle=vignette;ctx.fillRect(0,0,w,h);
    if(t>=6&&t<16){
      const k=t-6, fade=smooth(k)*smooth(16-t);
      ctx.globalAlpha=fade;moth(w*(.5+.25*Math.sin(k*.68)),h*(.5+.065*Math.sin(k*.93)),t,1.25*unit);ctx.globalAlpha=1;
    }else if(t>=16&&t<24){
      const k=t-16,x=w*(.5+.19*Math.sin(k*.62)),y=h*(.48+.035*Math.cos(k*.7));
      ctx.globalAlpha=smooth(k)*smooth(24-t);glow(x,y,100*unit,'rgba(211,177,103,.25)');
      ctx.save();ctx.translate(x,y);ctx.rotate(t*.6);ctx.fillStyle='#fff0c0';ctx.beginPath();ctx.moveTo(0,-38*unit);ctx.lineTo(29*unit,0);ctx.lineTo(0,38*unit);ctx.lineTo(-29*unit,0);ctx.closePath();ctx.fill();ctx.restore();ctx.globalAlpha=1;
    }else if(t>=24&&t<30){
      const k=t-24;
      // A continuous fast sweep through the centre, within retina motion bounds.
      ctx.globalAlpha=smooth(k/.4)*smooth((30-t)/.7);
      moth(w*(.5+.36*Math.sin(k*3.6)),h*(.51+.02*Math.sin(k*2)),t,1.5*unit);ctx.globalAlpha=1;
    }else if(t>=30&&t<36){
      const k=t-30,x=w*(.5+.28*Math.sin(k*.7)),y=h*(.46+.09*Math.sin(k*.9));
      ctx.globalAlpha=smooth(k)*smooth(36-t);moth(x,y,t,1.1*unit);ctx.globalAlpha=1;
      // Comet trails are scenery; the moth remains the brightest moving object.
      for(let i=0;i<4;i++){const u=(k*.15+i*.24)%1,x=w*u,y=h*(.37+i*.05);ctx.strokeStyle='rgba(133,156,177,.35)';ctx.lineWidth=unit;ctx.beginPath();ctx.moveTo(x-35*unit,y-20*unit);ctx.lineTo(x,y);ctx.stroke();}
    }
    hasFrame=true;retinaFrame();
  }
  function retinaFrame(){
    eye.fillStyle='#000';eye.fillRect(0,0,48,32);eye.drawImage(canvas,0,0,48,32);
    const image=eye.getImageData(0,0,48,32), pixels=[];
    let bright=0,peak=0,total=0,x=0,y=0;
    for(let i=0;i<image.data.length;i+=4){const value=Math.round(image.data[i]*.2126+image.data[i+1]*.7152+image.data[i+2]*.0722);pixels.push(value);image.data[i]=image.data[i+1]=image.data[i+2]=value;peak=Math.max(peak,value);const weight=Math.max(0,value-180);if(weight)bright++;total+=weight;x+=(i/4%48+.5)*weight;y+=(Math.floor(i/4/48)+.5)*weight;}
    $('retina-detail').textContent=bright?'Light detected · '+bright+' bright pixels':'Quiet scene · no bright target';
    $('retina-detail').dataset.peak=String(peak);$('retina-detail').dataset.weight=String(total);
    $('retina-detail').dataset.x=total?String(x/total/48):'';$('retina-detail').dataset.y=total?String(y/total/32):'';
    eye.putImageData(image,0,0);return {Width:48,Height:32,Pixels:pixels,CinemaStage:true};
  }
  function titles(){
    let index=0;for(let i=1;i<chapters.length;i++)if(time>=chapters[i][0])index=i;
    if(index!==current){current=index;const c=chapters[index];$('chapter').textContent=c[2];$('headline').innerHTML=c[3];$('subtitle').textContent=c[4];$('chapter-number').textContent=c[1]+' — VI';$('scene').value=String(c[0]);$('titles').classList.remove('change');void $('titles').offsetWidth;$('titles').classList.add('change');}
    $('scrub').value=time;$('progress').style.width=(time/duration*100)+'%';$('elapsed').textContent='00:'+String(Math.floor(time)).padStart(2,'0');
    $('play').textContent=playing?'Pause':'Play';
  }
  function clean(on){document.body.classList.toggle('recording',on);if(on&&document.activeElement instanceof HTMLElement)document.activeElement.blur();}
  function cancelCount(){countdownUntil=0;$('countdown').hidden=true;}
  function seek(value){cancelCount();time=clamp(Number(value),0,duration);draw(time);titles();}
  function toggle(){cancelCount();if(time>=duration)time=0;playing=!playing;titles();}
  function roll(){seek(0);playing=false;clean(true);countdownUntil=performance.now()+3000;$('countdown').hidden=false;titles();}
  function showStatus(s){
    live=true;statusTime=performance.now();
    const watching=s.Enabled&&s.Seeing&&!s.Blocked;
    $('connection').classList.toggle('live',!!watching);
    $('status').textContent=s.Preview?'Circuit preview · no desktop bird':!s.Enabled?'Turn on his nervous system':s.Sleeping?'Corbie is sleeping':s.Paused?'Corbie is paused':s.Flying?'Corbie is airborne':s.Blocked?'Connected · Corbie is busy':watching?(s.Hop>.05?'A little startle':s.Attention>.025?'Something caught his eye':'Corbie is watching'):'Connected · waiting for his eyes';
    $('attention').textContent=Math.round(clamp(s.Attention||0)*100)+'%';$('wariness').textContent=Math.round(clamp(s.Wariness||0)*100)+'%';
    walls=!!s.CinemaWalls;selecting=!!s.CinemaPicking;
    $('film-note').textContent=walls?'CORBIE · ON SET':'CORBIE · VOL. 01';
    $('wall-status').textContent=selecting?'Draw a box around the recording frame. Escape cancels.':walls?'Walls set · walking and flights stay inside':'Walls open · press B to set the boundary';
    $('release-walls').disabled=!walls&&!selecting;
  }
  function layoutKey(){const r=$('stage').getBoundingClientRect();return [screenX,screenY,devicePixelRatio,r.left,r.top,r.width,r.height].map(x=>Math.round(x*10)/10).join(',');}
  async function wallCommand(action){
    if(!local||!cinemaToken){$('wall-status').textContent='Open cinema from Corbie v0.100’s Nest to set walls.';return;}
    changingWalls=true;
    try{const r=await fetch('/api/cinema',{method:'POST',headers:{'Content-Type':'application/json','X-Corbie-Token':cinemaToken},body:JSON.stringify({Action:action}),signal:AbortSignal.timeout(1200)});if(!r.ok)throw Error();}
    catch{$('wall-status').textContent='Could not change walls. Open the cinema from v0.100’s Nest.';}
    finally{changingWalls=false;}
  }
  async function setWalls(){if(!local||!cinemaToken){await wallCommand('select');return;}cancelCount();playing=false;clean(true);layout=layoutKey();await wallCommand('select');}
  function disconnected(){live=false;$('connection').classList.remove('live');$('attention').textContent=$('wariness').textContent='—';$('status').textContent=local?'Open cinema from Corbie v0.100’s Nest':'Film preview · open from the Nest for live reactions';}
  async function exchange(){
    if(!local||busy||document.hidden||!hasFrame)return;
    busy=true;
    try{
      const response=await fetch('/api/state',{cache:'no-store',signal:AbortSignal.timeout(1200)});
      if(!response.ok)throw Error('offline');const data=await response.json();
      if(typeof data.token!=='string'||!data.state)throw Error('invalid state');
      cinemaToken=data.token;
      const nextLayout=layoutKey();
      if(layout&&nextLayout!==layout&&(walls||selecting)&&!changingWalls){await wallCommand('release');walls=false;}
      layout=nextLayout;
      const sent=await fetch('/api/retina',{method:'POST',headers:{'Content-Type':'application/json','X-Corbie-Token':data.token},body:JSON.stringify(retinaFrame()),signal:AbortSignal.timeout(1200)});
      if(!sent.ok&&sent.status!==429)throw Error('retina rejected');showStatus(data.state);
    }catch{disconnected();}finally{busy=false;}
  }
  $('play').addEventListener('click',toggle);$('restart').addEventListener('click',()=>seek(0));
  $('clean').addEventListener('click',()=>clean(true));
  $('set-walls').addEventListener('click',setWalls);$('release-walls').addEventListener('click',()=>wallCommand('release'));
  $('roll').addEventListener('click',roll);
  $('scrub').addEventListener('input',e=>seek(e.target.value));$('scene').addEventListener('change',e=>seek(e.target.value));
  $('guide').addEventListener('change',e=>{$('perch-guide').hidden=!e.target.checked;});
  $('format').addEventListener('change',e=>{const wide=e.target.value==='wide';$('stage').classList.toggle('wide',wide);canvas.width=wide?1280:720;canvas.height=wide?720:1280;draw(time);});
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'){cancelCount();clean(false);return;}
    if(/INPUT|SELECT/.test(e.target.tagName))return;
    if(e.code==='Space'&&e.target.tagName==='BUTTON')return;
    if(e.code==='Space'){e.preventDefault();toggle();}
    else if(e.key.toLowerCase()==='h')clean(!document.body.classList.contains('recording'));
    else if(e.key.toLowerCase()==='r')seek(0);
    else if(e.key.toLowerCase()==='c')roll();
    else if(e.key.toLowerCase()==='b')setWalls();
    else if(e.key.toLowerCase()==='o')wallCommand('release');
    else if(/^[1-6]$/.test(e.key))seek(chapters[Number(e.key)-1][0]);
  });
  document.addEventListener('visibilitychange',()=>{last=performance.now();if(document.hidden){cancelCount();playing=false;titles();}});
  function animate(now){
    const dt=last?Math.min(.1,(now-last)/1000):0;last=now;
    if(countdownUntil){const left=countdownUntil-now;$('countdown').textContent=String(Math.ceil(Math.max(0,left)/1000));if(left<=0){cancelCount();playing=true;}}
    if(playing&&!document.hidden){time+=dt;if(time>=duration){time=$('loop').checked?time%duration:duration;if(!$('loop').checked)playing=false;}}
    if(now-painted>1000/(reduced&&!playing?10:30)){draw(time);titles();painted=now;}
    if(live&&now-statusTime>2000)disconnected();
    requestAnimationFrame(animate);
  }
  disconnected();draw(0);titles();requestAnimationFrame(animate);setInterval(exchange,100);exchange();
})();
