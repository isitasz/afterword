const mediaTypes = {
  books: {label:'📖 books', singular:'book', route:'/api/books', fields:[
    {name:'title',label:'Title',type:'text',required:true},
    {name:'tags',label:'Tags',type:'text',placeholder:'fantasy, adventure'},
    {name:'rating',label:'Rating (0 to 5)',type:'number',min:0,max:5,step:.5,required:true},
    {name:'physical',label:'Physical copy',type:'checkbox'},
    {name:'dateRead',label:'Date',type:'date'},
    {name:'review',label:'Review',type:'textarea'}]},
  games: {label:'🎮 games', singular:'game', route:'/api/games', fields:[
    {name:'title',label:'Title',type:'text',required:true},
    {name:'hours',label:'Hours played',type:'text',placeholder:'e.g. 12.5'},
    {name:'rating',label:'Rating (0 to 5)',type:'number',min:0,max:5,step:.5,required:true},
    {name:'owned',label:'Owned?',type:'checkbox'},
    {name:'datePlayed',label:'Date',type:'date'},
    {name:'review',label:'Review',type:'textarea'}]},
  movies: {label:'🎬 movies', singular:'movie', route:'/api/movies', fields:[
    {name:'title',label:'Title',type:'text',required:true},
    {name:'rating',label:'Rating (0 to 5)',type:'number',min:0,max:5,step:.5,required:true},
    {name:'cinema',label:'At the cinema?',type:'checkbox'},
    {name:'dateWatched',label:'Date',type:'date'},
    {name:'review',label:'Review',type:'textarea'}]},
  animes: {label:'🎐 anime', singular:'anime', route:'/api/animes', fields:[
    {name:'title',label:'Title',type:'text',required:true},
    {name:'animeType',label:'Dubbed or subbed?',type:'select',options:[['Dubbed','Dubbed'],['Subbed','Subbed']]},
    {name:'rating',label:'Rating (0 to 5)',type:'number',min:0,max:5,step:.5,required:true},
    {name:'manga',label:'Read the manga?',type:'checkbox'},
    {name:'dateWatched',label:'Date',type:'date'},
    {name:'review',label:'Review',type:'textarea'}]}
};
const labels = {GoogleBooksApiKey:'Google Books API key',TmdbApiKey:'TMDB API key',RawgApiKey:'RAWG API key'};
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g,ch=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
const message = (id,text='',kind='') => { const element=document.getElementById(id); if(element){element.textContent=text;element.className=`form-message ${kind}`.trim();} };
let activeSection='books';
let editing=null;

async function api(url, options={}) {
  const method=(options.method||'GET').toUpperCase();
  const headers={...(options.body?{'Content-Type':'application/json'}:{}),...(options.headers||{})};
  if(method==='GET'){headers.Pragma='no-cache';headers['Cache-Control']='no-cache';}
  const response=await fetch(url,{...options,cache:method==='GET'?'no-store':options.cache,credentials:'same-origin',headers});
  if(response.status===401){showLogin();throw new Error('Your session expired. Please sign in again.');}
  if(!response.ok){let detail=`Error ${response.status}`;try{const body=await response.json();detail=body.detail||body.message||body.title||detail;}catch{}throw new Error(detail);}
  if(response.status===204)return null;
  const contentType=response.headers.get('content-type')||'';
  return contentType.includes('application/json')?response.json():null;
}
function showLogin(){document.getElementById('dashboard').classList.add('hidden');document.getElementById('login-view').classList.remove('hidden');}
function showDashboard(){document.getElementById('login-view').classList.add('hidden');document.getElementById('dashboard').classList.remove('hidden');}

document.getElementById('login-form').addEventListener('submit',async event=>{
  event.preventDefault();message('login-message','Signing in...');
  const form=new FormData(event.currentTarget);
  try{await api('/api/admin/login',{method:'POST',body:JSON.stringify({username:form.get('username'),password:form.get('password')})});event.currentTarget.reset();showDashboard();renderNav();await showSection('books');}
  catch(error){message('login-message',error.message,'error');}
});
document.getElementById('logout').addEventListener('click',async()=>{try{await api('/api/admin/logout',{method:'POST'});}catch{}showLogin();});

function renderNav(){
  const nav=document.getElementById('admin-nav');
  nav.innerHTML=[...Object.entries(mediaTypes).map(([key,type])=>`<button type="button" data-section="${key}">${type.label}</button>`),'<button type="button" data-section="settings">⚙️ settings</button>'].join('');
  nav.addEventListener('click',event=>{const button=event.target.closest('[data-section]');if(button)showSection(button.dataset.section);});
}
async function showSection(section){
  activeSection=section;editing=null;message('admin-message');
  document.querySelectorAll('#admin-nav button').forEach(button=>button.classList.toggle('active',button.dataset.section===section));
  if(section==='settings'){await renderSettings();return;}
  await renderMediaSection(section);
}
function fieldMarkup(field){
  if(field.type==='checkbox')return `<label class="check-field"><input name="${field.name}" type="checkbox"> ${field.label}</label>`;
  let control='';
  if(field.type==='textarea')control=`<textarea name="${field.name}" placeholder="Write your review..."></textarea>`;
  else if(field.type==='select')control=`<select name="${field.name}">${field.options.map(([value,label])=>`<option value="${escapeHtml(value)}">${escapeHtml(label)}</option>`).join('')}</select>`;
  else control=`<input name="${field.name}" type="${field.type}" ${field.required?'required':''} ${field.min!==undefined?`min="${field.min}"`:''} ${field.max!==undefined?`max="${field.max}"`:''} ${field.step!==undefined?`step="${field.step}"`:''} placeholder="${escapeHtml(field.placeholder||'')}">`;
  return `<label class="field"><span>${field.label}</span>${control}</label>`;
}
async function renderMediaSection(section){
  const type=mediaTypes[section];
  document.getElementById('admin-content').innerHTML=`<div class="admin-grid"><section class="admin-panel"><h2 id="form-title">Add ${type.singular}</h2><form id="media-form" class="form-stack">${type.fields.map(fieldMarkup).join('')}<div class="admin-form-actions"><button class="button primary" type="submit" id="save-media">search and add</button><button class="button ghost hidden" type="button" id="cancel-edit">cancel edit</button></div><div id="media-form-message" class="form-message" role="status"></div></form></section><section class="admin-panel"><h2>Items in catalog</h2><div id="item-list" class="item-list"><p class="settings-note">Loading...</p></div></section></div>`;
  document.getElementById('media-form').addEventListener('submit',saveMedia);
  document.getElementById('cancel-edit').addEventListener('click',resetMediaForm);
  await loadMediaList(section);
}
async function loadMediaList(section){
  const type=mediaTypes[section], list=document.getElementById('item-list');
  try{
    const items=await api(type.route);
    if(!items.length){list.innerHTML='<p class="settings-note">No items yet.</p>';return;}
    list.innerHTML=items.map((item,index)=>{
      const thumb=item.cover||item.poster||'';
      const safeThumb=/^(https:\/\/|\/api\/)/i.test(thumb)?escapeHtml(thumb):'';
      const date=item.dateRead||item.datePlayed||item.dateWatched||'No date';
      const refreshButton=section==='books'&&!item.cover?`<button type="button" class="button ghost" data-refresh-cover="${index}">find cover</button>`:'';
      return `<article class="admin-item"><img class="admin-thumb" src="${safeThumb}" alt="" loading="lazy"><div><strong title="${escapeHtml(item.title)}">${escapeHtml(item.title)}</strong><small>${escapeHtml(date)} · ${Number(item.rating||0).toFixed(1)} / 5</small></div><div class="item-buttons">${refreshButton}<button type="button" class="button ghost" data-edit="${index}">edit</button><button type="button" class="button danger" data-delete="${index}">delete</button></div></article>`;
    }).join('');
    list.onclick=event=>{
      const edit=event.target.closest('[data-edit]');const remove=event.target.closest('[data-delete]');
      const refresh=event.target.closest('[data-refresh-cover]');
      if(edit)beginEdit(section,items[Number(edit.dataset.edit)]);
      if(remove)deleteMedia(section,items[Number(remove.dataset.delete)]);
      if(refresh)refreshBookCover(items[Number(refresh.dataset.refreshCover)]);
    };
  }catch(error){list.innerHTML=`<p class="form-message error">${escapeHtml(error.message)}</p>`;}
}
function beginEdit(section,item){
  editing=item;
  const type=mediaTypes[section],form=document.getElementById('media-form');
  for(const field of type.fields){
    const control=form.elements.namedItem(field.name);let value=item[field.name];
    if(field.name==='tags'&&Array.isArray(value))value=value.join(', ');
    if(field.type==='checkbox')control.checked=Boolean(value);else control.value=value??'';
  }
  document.getElementById('form-title').textContent=`Edit ${type.singular}`;
  document.getElementById('save-media').textContent='save changes';
  document.getElementById('cancel-edit').classList.remove('hidden');
  document.getElementById('media-form').scrollIntoView({behavior:'smooth',block:'start'});
}
function resetMediaForm(){editing=null;document.getElementById('media-form').reset();const type=mediaTypes[activeSection];document.getElementById('form-title').textContent=`Add ${type.singular}`;document.getElementById('save-media').textContent='search and add';document.getElementById('cancel-edit').classList.add('hidden');message('media-form-message');}
async function saveMedia(event){
  event.preventDefault();const form=event.currentTarget,type=mediaTypes[activeSection];
  const payload=editing?{...editing}:{};
  for(const field of type.fields){const control=form.elements.namedItem(field.name);if(field.type==='checkbox')payload[field.name]=control.checked;else if(field.name==='rating')payload[field.name]=Number(control.value);else if(field.name==='tags')payload[field.name]=control.value.split(',').map(tag=>tag.trim()).filter(Boolean);else payload[field.name]=control.value.trim();}
  const button=document.getElementById('save-media');button.disabled=true;message('media-form-message',editing?'Saving...':'Searching metadata and saving...');
  try{
    if(editing)await api(`${type.route}/${encodeURIComponent(editing.id)}`,{method:'PUT',body:JSON.stringify(payload)});
    else await api(type.route,{method:'POST',body:JSON.stringify(payload)});
    resetMediaForm();message('admin-message',`${type.singular} saved successfully.`,'success');await loadMediaList(activeSection);
  }catch(error){message('media-form-message',error.message,'error');}
  finally{button.disabled=false;}
}
async function deleteMedia(section,item){
  const type=mediaTypes[section];if(!confirm(`Delete “${item.title}” permanently?`))return;
  try{await api(`${type.route}/${encodeURIComponent(item.id)}`,{method:'DELETE'});message('admin-message',`${type.singular} deleted.`,'success');await loadMediaList(section);}
  catch(error){message('admin-message',error.message,'error');}
}
async function refreshBookCover(item){
  message('admin-message',`Finding a cover for “${item.title}”...`);
  try{
    await api(`/api/books/${encodeURIComponent(item.id)}/cover/refresh`,{method:'POST',body:JSON.stringify({title:item.title})});
    message('admin-message','Cover saved to Neon.','success');
    await loadMediaList('books');
  }catch(error){message('admin-message',error.message,'error');}
}
async function renderSettings(){
  document.getElementById('admin-content').innerHTML=`<section class="admin-panel"><h2>API keys</h2><p class="settings-note">Keys are sent directly to the server and stored encrypted in Neon. Fields stay empty even when a key is already configured. Leave a field empty to keep its current value.</p><form id="settings-form" class="form-stack">${Object.entries(labels).map(([key,label])=>`<label class="field"><span>${label} <small class="key-state" id="state-${key}"></small></span><input name="${key}" type="password" autocomplete="new-password" placeholder="Paste a new key to replace the current one"></label>`).join('')}<div class="admin-form-actions"><button id="save-settings" class="button primary" type="submit">save keys</button></div><div id="settings-message" class="form-message" role="status"></div></form><p class="settings-note">The server needs the secret environment variable <code>APP_SETTINGS_ENCRYPTION_KEY</code> to encrypt keys in the database. It is separate from your API keys and must never be committed to GitHub.</p></section>`;
  try{
    const settings=await api('/api/admin/settings');
    for(const [key,label] of Object.entries(labels))document.getElementById(`state-${key}`).textContent=settings[`${key[0].toLowerCase()}${key.slice(1)}Configured`]? '· configured on server':'· not configured';
  }catch(error){message('settings-message',error.message,'error');}
  document.getElementById('settings-form').addEventListener('submit',async event=>{
    event.preventDefault();const form=new FormData(event.currentTarget);const body={googleBooksApiKey:form.get('GoogleBooksApiKey'),tmdbApiKey:form.get('TmdbApiKey'),rawgApiKey:form.get('RawgApiKey')};
    const button=document.getElementById('save-settings');button.disabled=true;message('settings-message','Saving encrypted keys...');
    try{const settings=await api('/api/admin/settings',{method:'PUT',body:JSON.stringify(body)});event.currentTarget.reset();for(const [key] of Object.entries(labels))document.getElementById(`state-${key}`).textContent=settings[`${key[0].toLowerCase()}${key.slice(1)}Configured`]? '· configured on server':'· not configured';message('settings-message','Settings saved.','success');}
    catch(error){message('settings-message',error.message,'error');}
    finally{button.disabled=false;}
  });
}

(async()=>{
  try{await api('/api/admin/session');showDashboard();renderNav();await showSection('books');}
  catch{showLogin();}
})();
