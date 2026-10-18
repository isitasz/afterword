const esc = value => String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
const imageUrl = value => {
  const url = String(value ?? '').trim();
  return /^(https:\/\/|\/(api|catalog-data)\/)/i.test(url) ? esc(url) : '';
};
const dateOf = item => item.dateRead || item.datePlayed || item.dateWatched || '';
const yearOf = item => /^\d{4}/.test(dateOf(item)) ? dateOf(item).slice(0,4) : 'sem-data';
const dateText = value => {
  if (!value) return 'No date';
  const match = String(value).match(/^(\d{4})-(\d{2})-(\d{2})/);
  return match ? `${match[3]}.${match[2]}.${match[1]}` : esc(value);
};
const stars = value => {
  const rating = Math.max(0, Math.min(5, Number(value) || 0));
  let html = '';
  for (let star = 1; star <= 5; star++) {
    if (rating >= star) html += '<span class="star filled">★</span>';
    else if (rating >= star - 0.5) html += '<span class="star half">★</span>';
    else html += '<span class="star empty">☆</span>';
  }
  return `<span class="rating" aria-label="Rating ${rating} out of 5">${html}<small>${rating.toFixed(1)}</small></span>`;
};
function mediaCard(item, kind) {
  const title = esc(item.title || 'Untitled');
  let image = kind === 'books' ? item.cover : item.poster;
  image = imageUrl(image);
  let subtitle = '';
  let info = '';
  let marker = '';
  let dateExtra = '';
  let tags = '';
  if (kind === 'books') {
    subtitle = [item.author && esc(item.author), item.year && `(${esc(item.year)})`].filter(Boolean).join(' · ');
    dateExtra = item.physical ? '📖 print copy' : '📱 digital';
    marker = item.physical ? '<div class="media-marker">▣ PHYSICAL</div>' : '';
    tags = item.tags?.length ? `<div class="book-tags">${item.tags.map(tag => `<span class="tag">${esc(tag)}</span>`).join('')}</div>` : '';
  } else if (kind === 'games') {
    info = `<span>${item.hours ? `${esc(item.hours)} hours` : 'hours not provided'}</span>`;
    marker = item.owned ? '<div class="media-marker">▣ OWNED</div>' : '';
  } else if (kind === 'movies') {
    info = item.year ? `<span>${esc(item.year)}</span>` : '';
    marker = item.cinema ? '<div class="media-marker">▣ AT THE CINEMA</div>' : '';
  } else {
    info = `<span>${esc(item.episodes || '—')} episodes</span><span class="info-divider">•</span><span>${esc(item.animeType || 'type not provided')}</span>`;
    marker = item.manga ? '<div class="media-marker">▣ MANGA</div>' : '';
  }
  const external = kind === 'games' && /^https?:\/\//i.test(item.link || '') ? `<a class="media-link" href="${esc(item.link)}" target="_blank" rel="noopener">Open on RAWG ↗</a>` : '';
  const cover = image ? `<img src="${image}" alt="Cover for ${title}" loading="lazy">` : '<div class="no-art">No image</div>';
  return `<article class="media-card"><div class="media-art"><div class="poster-box">${cover}</div>${marker}</div><div class="media-body"><div class="media-date"><span>${dateText(dateOf(item))}</span>${dateExtra ? `<span class="date-extra">${dateExtra}</span>` : ''}</div><div class="media-heading"><h2 class="media-title">${title}</h2>${stars(item.rating)}</div>${subtitle ? `<p class="media-subtitle">${subtitle}</p>` : ''}${info ? `<div class="media-info">${info}${external}</div>` : external ? `<div class="media-info">${external}</div>` : ''}${tags}<div class="review">${esc(item.review || 'No review')}</div></div></article>`;
}
function renderCatalog(target, kind, entries) {
  entries.sort((a,b) => dateOf(b).localeCompare(dateOf(a)) || String(a.title).localeCompare(String(b.title)));
  if (!entries.length) { target.innerHTML = '<div class="empty-state">There are no entries in this catalog yet.</div>'; return; }
  const groups = new Map();
  for (const item of entries) {
    const year = yearOf(item);
    if (!groups.has(year)) groups.set(year, []);
    groups.get(year).push(item);
  }
  const years = [...groups.keys()].sort((a,b) => a === 'sem-data' ? 1 : b === 'sem-data' ? -1 : Number(b)-Number(a));
  document.getElementById('nav-anos').innerHTML = years.map(year => `<a href="#year-${esc(year)}">${year === 'sem-data' ? 'No date' : year}</a>`).join('');
  target.innerHTML = years.map(year => `<section class="year-section" id="year-${esc(year)}"><h2 class="year-heading">${year === 'sem-data' ? 'No date' : year}</h2><p class="year-stats">${groups.get(year).length} ${kind === 'books' ? 'book(s)' : kind === 'games' ? 'game(s)' : kind === 'movies' ? 'movie(s)' : 'anime'} logged</p>${groups.get(year).map(item => mediaCard(item,kind)).join('')}</section>`).join('');
}

async function loadCatalog() {
  const target = document.getElementById('catalog');
  if (!target) return;
  const kind = target.dataset.kind;
  const localDevelopment = ['localhost', '127.0.0.1'].includes(window.location.hostname);
  const routes = localDevelopment
    ? {books:'/api/books',games:'/api/games',movies:'/api/movies',animes:'/api/animes'}
    : {books:'/catalog-data/books.json',games:'/catalog-data/games.json',movies:'/catalog-data/movies.json',animes:'/catalog-data/animes.json'};
  const route = routes[kind];
  const cacheKey = `afterword.catalog.${kind}`;
  let hasSavedCopy = false;
  try {
    const savedEntries = JSON.parse(localStorage.getItem(cacheKey) || 'null');
    if (Array.isArray(savedEntries)) {
      renderCatalog(target, kind, savedEntries);
      hasSavedCopy = true;
    }
  } catch {}
  if (!hasSavedCopy) target.innerHTML = '<div class="empty-state">Loading catalog...</div>';

  try {
    const response = await fetch(route);
    if (!response.ok) throw new Error(`The server returned ${response.status}.`);
    const entries = await response.json();
    renderCatalog(target, kind, entries);
    try { localStorage.setItem(cacheKey, JSON.stringify(entries)); } catch {}
  } catch (error) {
    if (!hasSavedCopy) {
      target.innerHTML = `<div class="error-state">Could not load this catalog.<br>${esc(error.message)}</div>`;
    } else {
      const note = document.createElement('p');
      note.className = 'empty-state';
      note.textContent = 'Showing the saved catalog. Live updates are temporarily unavailable.';
      target.prepend(note);
    }
  }
}
document.querySelector('.top-button')?.addEventListener('click',()=>window.scrollTo({top:0,behavior:'smooth'}));
loadCatalog();
