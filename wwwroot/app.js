/* ==========================================================================
   CineStream Desktop - Frontend Logic & Video Player Engine
   ========================================================================== */

// Global error listener to pipe renderer errors to terminal
window.addEventListener('error', (event) => {
  const msg = `[Renderer Error] ${event.message} at ${event.filename}:${event.lineno}:${event.colno}`;
  if (window.electronAPI && window.electronAPI.logToTerminal) {
    window.electronAPI.logToTerminal(msg);
  } else {
    console.error(msg);
  }
});

// 1. Movie Library Database
const MOVIE_DATABASE = [
  {
    id: "tears-of-steel",
    title: "Tears of Steel",
    year: "2012",
    rating: "8.2",
    duration: "12 mins",
    director: "Ian Hubert",
    cast: "Derek de Lint, Sergio Hasselbaink, Rogier Schippers",
    genres: ["Sci-Fi", "VFX", "Action"],
    plot: "A giant robot weapon system stomps through a futuristic, dystopian Amsterdam. A team of scientists and fighters scramble to stop it, fueled by the heartbreak and lingering memories of a past romance.",
    poster: "https://images.unsplash.com/photo-1589254065878-42c9da997008?w=400&q=80",
    backdrop: "https://images.unsplash.com/photo-1589254065878-42c9da997008?w=1200&q=80",
    videoUrl: "https://ia801400.us.archive.org/23/items/Tears-of-Steel/tears_of_steel_1080p.mp4",
    category: "scifi"
  },
  {
    id: "sintel",
    title: "Sintel",
    year: "2010",
    rating: "7.8",
    duration: "15 mins",
    director: "Colin Levy",
    cast: "Halina Reijn, Thom Hoffman",
    genres: ["Fantasy", "Adventure", "Animated"],
    plot: "Sintel, a lonely young woman, befriended a baby dragon named Scales. When the dragon is snatched by a gargantuan adult beast, Sintel embarks on a dangerous and emotional quest to find her companion.",
    poster: "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=400&q=80",
    backdrop: "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=1200&q=80",
    videoUrl: "https://media.w3.org/2010/05/sintel/trailer.mp4",
    category: "animated"
  },
  {
    id: "big-buck-bunny",
    title: "Big Buck Bunny",
    year: "2008",
    rating: "7.5",
    duration: "10 mins",
    director: "Sacha Goedegebure",
    cast: "Bunny, Squirrels, Rodents",
    genres: ["Comedy", "Cartoon", "Animated"],
    plot: "A large and lovable rabbit named Bunny wakes up to enjoy a peaceful forest day. When three obnoxious rodents target him and his forest friends, he decides to orchestrate a hilarious, elaborate revenge scheme.",
    poster: "https://images.unsplash.com/photo-1507679799987-c73779587ccf?w=400&q=80",
    backdrop: "https://images.unsplash.com/photo-1507679799987-c73779587ccf?w=1200&q=80",
    videoUrl: "https://media.w3.org/2010/05/bunny/trailer.mp4",
    category: "animated"
  },
  {
    id: "elephants-dream",
    title: "Elephant's Dream",
    year: "2006",
    rating: "7.2",
    duration: "11 mins",
    director: "Bassam Kurdali",
    cast: "Tygo Gernandt, Cas Jansen",
    genres: ["Sci-Fi", "Surreal", "Animated"],
    plot: "Two men, Proog and Emo, live in a massive, chaotic machine world, which responds to their thoughts and fears. As they struggle to understand their environment, their friendship begins to unravel.",
    poster: "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=400&q=80",
    backdrop: "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=1200&q=80",
    videoUrl: "https://dn710604.ca.archive.org/0/items/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4",
    category: "scifi"
  },
  {
    id: "for-bigger-blazes",
    title: "For Bigger Blazes",
    year: "2015",
    rating: "6.8",
    duration: "1 min",
    director: "Google Open Media",
    cast: "Chromecast Fire Team",
    genres: ["Action", "Shorts"],
    plot: "An action-packed look at extreme fires, specialized tools, and brave responders who jump into the blazes to protect homes and nature. Cinematic high-speed footage.",
    poster: "https://images.unsplash.com/photo-1508873696983-2df519f0397e?w=400&q=80",
    backdrop: "https://images.unsplash.com/photo-1508873696983-2df519f0397e?w=1200&q=80",
    videoUrl: "https://media.w3.org/2010/05/video/movie_300.mp4",
    category: "shorts"
  },
  {
    id: "for-bigger-fun",
    title: "For Bigger Fun",
    year: "2016",
    rating: "7.0",
    duration: "1 min",
    director: "Google Open Media",
    cast: "Beach Surfers, Extreme Sports Athletes",
    genres: ["Sports", "Shorts", "Adventure"],
    plot: "A visual celebration of extreme water sports, high-speed surfing, skimboarding, and cliff diving, highlighting the beauty of coastal waves and the thrill of speed.",
    poster: "https://images.unsplash.com/photo-1502680390469-be75c86b636f?w=400&q=80",
    backdrop: "https://images.unsplash.com/photo-1502680390469-be75c86b636f?w=1200&q=80",
    videoUrl: "https://media.w3.org/2010/05/sintel/trailer.mp4",
    category: "shorts"
  }
];

// App State Management
let watchlist = JSON.parse(localStorage.getItem('cinestream_watchlist')) || [];
let activeMovie = MOVIE_DATABASE[0]; // Default spotlight movie

// DOM Elements
const elements = {
  // Navigation
  navItems: document.querySelectorAll('.nav-item'),
  panels: document.querySelectorAll('.view-panel'),
  
  // Gallery rows
  rowSciFi: document.getElementById('row-scifi'),
  rowAnimated: document.getElementById('row-animated'),
  rowShorts: document.getElementById('row-shorts'),
  watchlistRow: document.getElementById('watchlist-row'),
  watchlistContainer: document.getElementById('watchlist-row-container'),
  watchlistCount: document.getElementById('watchlist-count'),
  
  // Spotlight
  heroBanner: document.getElementById('hero-banner'),
  heroTitle: document.getElementById('hero-title'),
  heroDescription: document.getElementById('hero-description'),
  heroPlayBtn: document.getElementById('hero-play-btn'),
  heroInfoBtn: document.getElementById('hero-info-btn'),
  
  // Details Modal
  detailsModal: document.getElementById('details-modal'),
  detailsClose: document.getElementById('details-close'),
  modalHeroBg: document.getElementById('modal-hero-bg'),
  modalTitle: document.getElementById('modal-title'),
  modalGenres: document.getElementById('modal-genres'),
  modalRating: document.getElementById('modal-rating'),
  modalYear: document.getElementById('modal-year'),
  modalDuration: document.getElementById('modal-duration'),
  modalPlot: document.getElementById('modal-plot'),
  modalDirector: document.getElementById('modal-director'),
  modalCast: document.getElementById('modal-cast'),
  modalPlayBtn: document.getElementById('modal-play-btn'),
  modalWatchlistBtn: document.getElementById('modal-watchlist-btn'),
  modalWatchlistIcon: document.getElementById('modal-watchlist-icon'),
  modalRecsGrid: document.getElementById('modal-recommended-grid'),
  
  // Search
  searchInput: document.getElementById('search-input'),
  watchlistToggleBtn: document.getElementById('watchlist-toggle-btn'),
  searchResultsPanel: document.getElementById('search-results-panel'),
  searchQueryText: document.getElementById('search-query-text'),
  searchResultsGrid: document.getElementById('search-results-grid'),
  closeSearchBtn: document.getElementById('close-search-btn'),
  galleryScroller: document.getElementById('gallery-scroller'),

  // Video Player
  playerModal: document.getElementById('player-modal'),
  playerContainer: document.getElementById('player-container'),
  video: document.getElementById('video-element'),
  playerOverlay: document.getElementById('player-overlay'),
  playerClose: document.getElementById('player-close'),
  playerVideoTitle: document.getElementById('player-video-title'),
  centerCue: document.getElementById('center-cue'),
  progressContainer: document.getElementById('progress-container'),
  progressBar: document.getElementById('progress-bar'),
  progressFilled: document.getElementById('progress-filled'),
  progressHover: document.getElementById('progress-hover'),
  progressHandle: document.getElementById('progress-handle'),
  playBtn: document.getElementById('player-play-btn'),
  playIcon: document.getElementById('play-icon'),
  pauseIcon: document.getElementById('pause-icon'),
  skipBack: document.getElementById('player-skip-back'),
  skipForward: document.getElementById('player-skip-forward'),
  muteBtn: document.getElementById('player-mute-btn'),
  volumeHighIcon: document.getElementById('volume-high-icon'),
  volumeMutedIcon: document.getElementById('volume-muted-icon'),
  volumeSlider: document.getElementById('volume-slider'),
  timeCurrent: document.getElementById('time-current'),
  timeDuration: document.getElementById('time-duration'),
  speedBtn: document.getElementById('player-speed-btn'),
  speedMenu: document.getElementById('speed-menu'),
  fullscreenBtn: document.getElementById('player-fullscreen-btn'),
  fullscreenEnterIcon: document.getElementById('fullscreen-enter-icon'),
  fullscreenExitIcon: document.getElementById('fullscreen-exit-icon')
};

// ==========================================================================
// Initialization
// ==========================================================================
document.addEventListener('DOMContentLoaded', () => {
  setupSpotlight(MOVIE_DATABASE[0]);
  renderGallery();
  setupWatchlistCount();
  setupEventListeners();

  // Handle mouse leaving the sidebar to hide it when a streaming service is active
  document.addEventListener('mouseleave', () => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({
        type: 'hoverSidebar',
        state: 'hide'
      }));
    }
  });
});

// ==========================================================================
// Nav Tab Switching & Lazy Webview Loader
// ==========================================================================
function switchTab(targetName) {
  // If it's the local CineStream theater video player, pause it
  const activePanel = document.querySelector('.view-panel.active');
  if (activePanel && activePanel.id === 'cinestream-view') {
    if (elements.video && !elements.video.paused) {
      elements.video.pause();
      updatePlayIcon(false);
    }
  }

  // Update sidebar active buttons
  elements.navItems.forEach(btn => {
    btn.classList.remove('active');
    if (btn.getAttribute('data-target') === targetName) {
      btn.classList.add('active');
    }
  });

  if (targetName === 'cinestream') {
    elements.panels[0].classList.add('active');
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({ type: 'hideAllServices' }));
    }
  } else {
    const serviceUrls = {
      youtube: 'https://www.youtube.com',
      netflix: 'https://www.netflix.com',
      twitch: 'https://www.twitch.tv',
      prime: 'https://www.primevideo.com',
      disney: 'https://www.disneyplus.com',
      max: 'https://www.max.com',
      hulu: 'https://www.hulu.com',
      paramount: 'https://www.paramountplus.com',
      peacock: 'https://www.peacocktv.com',
      starz: 'https://www.starz.com',
      apple: 'https://tv.apple.com',
      spotify: 'https://open.spotify.com',
      pandora: 'https://www.pandora.com',
      applemusic: 'https://music.apple.com',
      tubi: 'https://tubitv.com',
      freevee: 'https://www.amazon.com/freevee',
      sling: 'https://www.sling.com/freestream',
      pluto: 'https://pluto.tv',
      plex: 'https://watch.plex.tv'
    };
    const url = serviceUrls[targetName] || 'https://www.google.com';

    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({
        type: 'showService',
        name: targetName,
        url: url
      }));
    }
  }
}

// ==========================================================================
// Gallery Rendering
// ==========================================================================
function renderGallery() {
  // Clear lists
  elements.rowSciFi.innerHTML = '';
  elements.rowAnimated.innerHTML = '';
  elements.rowShorts.innerHTML = '';
  
  MOVIE_DATABASE.forEach(movie => {
    const card = createMovieCard(movie);
    
    if (movie.category === 'scifi') {
      elements.rowSciFi.appendChild(card);
    } else if (movie.category === 'animated') {
      elements.rowAnimated.appendChild(card);
    } else if (movie.category === 'shorts') {
      elements.rowShorts.appendChild(card);
    }
  });

  renderWatchlist();
}

function createMovieCard(movie) {
  const card = document.createElement('div');
  card.className = 'movie-card';
  card.id = `card-${movie.id}`;
  card.innerHTML = `
    <img src="${movie.poster}" alt="${movie.title}" class="movie-card-img" loading="lazy">
    <div class="movie-card-overlay">
      <h3 class="movie-card-title">${movie.title}</h3>
      <div class="movie-card-meta">
        <span class="card-rating">★ ${movie.rating}</span>
        <span>•</span>
        <span>${movie.year}</span>
        <span>•</span>
        <span>${movie.duration}</span>
      </div>
    </div>
  `;
  
  card.addEventListener('click', () => openDetails(movie));
  return card;
}

// Spotlight Header Setup
function setupSpotlight(movie) {
  elements.heroBanner.style.backgroundImage = `url(${movie.backdrop})`;
  elements.heroTitle.textContent = movie.title;
  elements.heroDescription.textContent = movie.plot;
  
  // Re-bind actions
  elements.heroPlayBtn.onclick = () => playMovie(movie);
  elements.heroInfoBtn.onclick = () => openDetails(movie);
}

// Watchlist Logic
function renderWatchlist() {
  elements.watchlistRow.innerHTML = '';
  
  if (watchlist.length === 0) {
    elements.watchlistContainer.classList.add('hidden');
    return;
  }
  
  elements.watchlistContainer.classList.remove('hidden');
  
  watchlist.forEach(movieId => {
    const movie = MOVIE_DATABASE.find(m => m.id === movieId);
    if (movie) {
      elements.watchlistRow.appendChild(createMovieCard(movie));
    }
  });
}

function toggleWatchlist(movieId) {
  const index = watchlist.indexOf(movieId);
  if (index === -1) {
    watchlist.push(movieId);
  } else {
    watchlist.splice(index, 1);
  }
  localStorage.setItem('cinestream_watchlist', JSON.stringify(watchlist));
  
  setupWatchlistCount();
  renderWatchlist();
  updateWatchlistBtnUI(movieId);
}

function setupWatchlistCount() {
  elements.watchlistCount.textContent = watchlist.length;
}

function updateWatchlistBtnUI(movieId) {
  const isBookmarked = watchlist.includes(movieId);
  if (isBookmarked) {
    elements.modalWatchlistBtn.classList.add('active');
  } else {
    elements.modalWatchlistBtn.classList.remove('active');
  }
}

// ==========================================================================
// Details Modal Logic
// ==========================================================================
function openDetails(movie) {
  activeMovie = movie;
  
  elements.modalHeroBg.style.backgroundImage = `url(${movie.backdrop})`;
  elements.modalTitle.textContent = movie.title;
  elements.modalGenres.textContent = movie.genres.join(' • ');
  elements.modalRating.textContent = `★ ${movie.rating}`;
  elements.modalYear.textContent = movie.year;
  elements.modalDuration.textContent = movie.duration;
  elements.modalPlot.textContent = movie.plot;
  elements.modalDirector.textContent = movie.director;
  elements.modalCast.textContent = movie.cast;
  
  // Setup Actions
  elements.modalPlayBtn.onclick = () => {
    closeDetails();
    playMovie(movie);
  };
  
  elements.modalWatchlistBtn.onclick = () => toggleWatchlist(movie.id);
  updateWatchlistBtnUI(movie.id);
  
  // Render recommendations (excluding current movie)
  elements.modalRecsGrid.innerHTML = '';
  const recommendations = MOVIE_DATABASE.filter(m => m.id !== movie.id).slice(0, 3);
  recommendations.forEach(rec => {
    const card = createMovieCard(rec);
    elements.modalRecsGrid.appendChild(card);
  });
  
  elements.detailsModal.style.display = 'flex';
}

function closeDetails() {
  elements.detailsModal.style.display = 'none';
}

// ==========================================================================
// Search Filtering
// ==========================================================================
function performSearch(query) {
  const cleanQuery = query.trim().toLowerCase();
  
  if (cleanQuery === '') {
    clearSearch();
    return;
  }
  
  // Hide main scroller, show results
  elements.galleryScroller.classList.add('hidden');
  elements.searchResultsPanel.classList.remove('hidden');
  elements.searchQueryText.textContent = query;
  
  elements.searchResultsGrid.innerHTML = '';
  
  const results = MOVIE_DATABASE.filter(movie => {
    return (
      movie.title.toLowerCase().includes(cleanQuery) ||
      movie.plot.toLowerCase().includes(cleanQuery) ||
      movie.genres.some(g => g.toLowerCase().includes(cleanQuery)) ||
      movie.cast.toLowerCase().includes(cleanQuery) ||
      movie.director.toLowerCase().includes(cleanQuery)
    );
  });
  
  if (results.length === 0) {
    elements.searchResultsGrid.innerHTML = `<div style="grid-column: 1/-1; padding: 40px; text-align: center; color: var(--text-sub);">No movies found matching "${query}".</div>`;
  } else {
    results.forEach(movie => {
      elements.searchResultsGrid.appendChild(createMovieCard(movie));
    });
  }
}

function clearSearch() {
  elements.searchInput.value = '';
  elements.searchResultsPanel.classList.add('hidden');
  elements.galleryScroller.classList.remove('hidden');
  renderWatchlist(); // Ensure watchlist reflects any updates
}

// ==========================================================================
// Custom Video Player Controller
// ==========================================================================
let controlsTimeout;
const CONTROLS_HIDE_DELAY = 3000;

function playMovie(movie) {
  elements.playerVideoTitle.textContent = movie.title;
  elements.video.src = movie.videoUrl;
  elements.playerModal.style.display = 'block';
  
  // Start playback
  elements.video.play()
    .then(() => updatePlayIcon(true))
    .catch(err => console.error("Playback error: ", err));

  // Reset controls
  elements.video.playbackRate = 1.0;
  elements.speedBtn.textContent = '1.0x';
  document.querySelectorAll('.speed-menu button').forEach(btn => btn.classList.remove('active'));
  document.querySelector('.speed-menu button[data-speed="1.0"]').classList.add('active');

  // Trigger controls overlay show
  showControlsOverlay();
}

function closeVideoPlayer() {
  elements.video.pause();
  elements.video.src = '';
  elements.playerModal.style.display = 'none';
  
  // Exit fullscreen if active
  if (document.fullscreenElement) {
    document.exitFullscreen().catch(err => console.error(err));
  }
  
  clearTimeout(elements.video.hideControlsTimer);
}

function togglePlay() {
  if (elements.video.paused) {
    elements.video.play();
    updatePlayIcon(true);
    animateCue('play');
  } else {
    elements.video.pause();
    updatePlayIcon(false);
    animateCue('pause');
  }
  showControlsOverlay();
}

function updatePlayIcon(isPlaying) {
  if (isPlaying) {
    elements.playIcon.classList.add('hidden');
    elements.pauseIcon.classList.remove('hidden');
  } else {
    elements.playIcon.classList.remove('hidden');
    elements.pauseIcon.classList.add('hidden');
  }
}

function animateCue(type) {
  elements.centerCue.classList.remove('animate');
  void elements.centerCue.offsetWidth; // Trigger reflow to restart animation
  
  const path = elements.centerCue.querySelector('svg path');
  if (type === 'play') {
    path.setAttribute('d', 'M8 5v14l11-7z'); // Play triangle SVG
  } else {
    path.setAttribute('d', 'M6 19h4V5H6v14zm8-14v14h4V5h-4z'); // Pause bars SVG
  }
  elements.centerCue.classList.add('animate');
}

// Progress Timeline SEEKING
function updateProgress() {
  const percent = (elements.video.currentTime / elements.video.duration) * 100;
  elements.progressFilled.style.width = `${percent}%`;
  elements.progressHandle.style.left = `${percent}%`;
  
  elements.timeCurrent.textContent = formatTime(elements.video.currentTime);
  if (!isNaN(elements.video.duration)) {
    elements.timeDuration.textContent = formatTime(elements.video.duration);
  }
}

function scrubVideo(e) {
  const scrubTime = (e.offsetX / elements.progressBar.offsetWidth) * elements.video.duration;
  if (!isNaN(scrubTime)) {
    elements.video.currentTime = scrubTime;
  }
}

function handleProgressHover(e) {
  const percent = (e.offsetX / elements.progressBar.offsetWidth) * 100;
  elements.progressHover.style.width = `${percent}%`;
}

// Volume Controls
function handleVolumeChange() {
  elements.video.volume = elements.volumeSlider.value;
  elements.video.muted = (elements.video.volume === 0);
  updateVolumeIcon();
}

function toggleMute() {
  elements.video.muted = !elements.video.muted;
  if (elements.video.muted) {
    elements.volumeSlider.value = 0;
  } else {
    elements.volumeSlider.value = elements.video.volume || 1;
  }
  updateVolumeIcon();
}

function updateVolumeIcon() {
  if (elements.video.muted || elements.video.volume === 0) {
    elements.volumeHighIcon.classList.add('hidden');
    elements.volumeMutedIcon.classList.remove('hidden');
  } else {
    elements.volumeHighIcon.classList.remove('hidden');
    elements.volumeMutedIcon.classList.add('hidden');
  }
}

// Fullscreen
function toggleFullscreen() {
  if (!document.fullscreenElement) {
    elements.playerContainer.requestFullscreen()
      .then(() => {
        elements.fullscreenEnterIcon.classList.add('hidden');
        elements.fullscreenExitIcon.classList.remove('hidden');
      })
      .catch(err => console.error(err));
  } else {
    document.exitFullscreen()
      .then(() => {
        elements.fullscreenEnterIcon.classList.remove('hidden');
        elements.fullscreenExitIcon.classList.add('hidden');
      })
      .catch(err => console.error(err));
  }
}

// Utilities
function formatTime(timeSeconds) {
  const minutes = Math.floor(timeSeconds / 60);
  const seconds = Math.floor(timeSeconds % 60);
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
}

// Controls Auto-Hide
function showControlsOverlay() {
  elements.playerOverlay.classList.remove('hidden');
  document.body.style.cursor = 'default';
  
  clearTimeout(controlsTimeout);
  
  if (!elements.video.paused) {
    controlsTimeout = setTimeout(() => {
      elements.playerOverlay.classList.add('hidden');
      document.body.style.cursor = 'none';
    }, CONTROLS_HIDE_DELAY);
  }
}

// ==========================================================================
// Event Listeners Setup
// ==========================================================================
function setupEventListeners() {
  // Navigation tabs
  elements.navItems.forEach(item => {
    item.addEventListener('click', () => {
      const target = item.getAttribute('data-target');
      switchTab(target);
    });
  });
  
  // Watchlist page shortcut
  elements.watchlistToggleBtn.addEventListener('click', () => {
    elements.galleryScroller.scrollTo({ top: 0, behavior: 'smooth' });
    // If search results are showing, close search to view local gallery
    if (!elements.searchResultsPanel.classList.contains('hidden')) {
      clearSearch();
    }
    
    // Quick flash highlight of the watchlist row
    setTimeout(() => {
      elements.watchlistContainer.scrollIntoView({ behavior: 'smooth', block: 'center' });
      elements.watchlistContainer.style.outline = '2px solid var(--accent-neon)';
      elements.watchlistContainer.style.borderRadius = '12px';
      setTimeout(() => { elements.watchlistContainer.style.outline = 'none'; }, 1000);
    }, 200);
  });
  
  // Search Events
  elements.searchInput.addEventListener('input', (e) => {
    performSearch(e.target.value);
  });
  
  elements.closeSearchBtn.addEventListener('click', clearSearch);
  
  // Details Modal close
  elements.detailsClose.addEventListener('click', closeDetails);
  
  window.addEventListener('click', (e) => {
    if (e.target === elements.detailsModal) {
      closeDetails();
    }
  });

  // ------------------------------------------------------------------------
  // Custom Media Player Events
  // ------------------------------------------------------------------------
  elements.video.addEventListener('timeupdate', updateProgress);
  elements.video.addEventListener('click', togglePlay);
  elements.video.addEventListener('doubleclick', toggleFullscreen);
  
  elements.playBtn.addEventListener('click', togglePlay);
  elements.playerClose.addEventListener('click', closeVideoPlayer);
  
  elements.skipBack.addEventListener('click', () => {
    elements.video.currentTime = Math.max(0, elements.video.currentTime - 10);
    showControlsOverlay();
  });
  
  elements.skipForward.addEventListener('click', () => {
    elements.video.currentTime = Math.min(elements.video.duration, elements.video.currentTime + 10);
    showControlsOverlay();
  });
  
  // Seek Timeline
  elements.progressBar.addEventListener('click', scrubVideo);
  elements.progressBar.addEventListener('mousemove', handleProgressHover);
  
  // Volume Slider
  elements.volumeSlider.addEventListener('input', handleVolumeChange);
  elements.muteBtn.addEventListener('click', toggleMute);
  
  // Fullscreen
  elements.fullscreenBtn.addEventListener('click', toggleFullscreen);
  
  // Playback speed menu
  elements.speedBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    elements.speedMenu.classList.toggle('hidden');
  });
  
  document.querySelectorAll('.speed-menu button').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const speed = parseFloat(btn.getAttribute('data-speed'));
      elements.video.playbackRate = speed;
      elements.speedBtn.textContent = speed === 1.0 ? 'Normal' : `${speed}x`;
      
      document.querySelectorAll('.speed-menu button').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      elements.speedMenu.classList.add('hidden');
    });
  });

  window.addEventListener('click', () => {
    elements.speedMenu.classList.add('hidden');
  });
  
  // Show controls on mouse movement
  elements.playerContainer.addEventListener('mousemove', showControlsOverlay);
  
  // F12 DevTools request for main webview
  window.addEventListener('keydown', (e) => {
    if (e.key === 'F12') {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ type: 'openDevTools' }));
      }
    }
  });

  // Keyboard Shortcuts in Player
  window.addEventListener('keydown', (e) => {
    if (elements.playerModal.style.display === 'block') {
      if (e.code === 'Space') {
        e.preventDefault();
        togglePlay();
      } else if (e.code === 'ArrowLeft') {
        e.preventDefault();
        elements.video.currentTime = Math.max(0, elements.video.currentTime - 10);
        showControlsOverlay();
      } else if (e.code === 'ArrowRight') {
        e.preventDefault();
        elements.video.currentTime = Math.min(elements.video.duration, elements.video.currentTime + 10);
        showControlsOverlay();
      } else if (e.code === 'ArrowUp') {
        e.preventDefault();
        elements.video.volume = Math.min(1, elements.video.volume + 0.05);
        elements.volumeSlider.value = elements.video.volume;
        updateVolumeIcon();
        showControlsOverlay();
      } else if (e.code === 'ArrowDown') {
        e.preventDefault();
        elements.video.volume = Math.max(0, elements.video.volume - 0.05);
        elements.volumeSlider.value = elements.video.volume;
        updateVolumeIcon();
        showControlsOverlay();
      } else if (e.code === 'KeyF') {
        e.preventDefault();
        toggleFullscreen();
      } else if (e.code === 'Escape') {
        e.preventDefault();
        closeVideoPlayer();
      }
    }
  });
}
