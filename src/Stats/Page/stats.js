// FCT stats page: sections, sortable tables, the treasure weeks, settings (only when FCT serves the page) and refresh.
(() => {
  const doc = document, body = doc.body;
  if (!doc.querySelector('[data-tab]')) return;
  doc.documentElement.classList.add('js'); // without script every section shows, one after another

  // Per browser tab only: the open section, the weeks, the sort order and the scroll survive the page's own updates.
  const memory = {
    get: key => { try { return sessionStorage.getItem('fct-' + key); } catch { return null; } },
    set: (key, value) => { try { sessionStorage.setItem('fct-' + key, value); } catch { /* storage off */ } },
  };
  const served = location.protocol === 'http:' || location.protocol === 'https:';
  let saving = false;
  if (served) doc.documentElement.classList.add('served');

  const toast = doc.querySelector('.toast');
  let toastTimer;
  const say = (message, failed) => {
    if (!toast) return;
    toast.textContent = message;
    toast.className = failed ? 'toast is-bad' : 'toast';
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toast.hidden = true; }, 2600);
  };
  const ping = () => fetch('/api/ping', { cache: 'no-store' });
  const post = (key, value) => fetch('/api/ajuste', {
    method: 'POST',
    headers: { 'X-FCT': '1', 'Content-Type': 'application/x-www-form-urlencoded' },
    body: encodeURIComponent(key) + '=' + encodeURIComponent(value),
  }).then(reply => reply.json()).then(answer => { if (!answer.ok) throw new Error(); });

  // ---- Scroll: the window and every list that scrolls inside its own card ------------------------------------------------
  // Each scrolled element is known by its place in the page, which an update keeps (only the figures inside change).
  const shell = () => doc.querySelector('.shell');
  const placeOf = element => {
    const steps = [];
    for (let node = element; node && node !== shell(); node = node.parentElement) {
      steps.push(node.id ? '#' + node.id : String([...node.parentElement.children].indexOf(node)));
      if (node.id) break;
    }
    return steps.reverse().join('/');
  };
  const elementAt = place => {
    const steps = place.split('/');
    let node = steps[0].startsWith('#') ? doc.getElementById(steps.shift().slice(1)) : shell();
    for (const step of steps) node = node && node.children[Number(step)];
    return node;
  };
  const scrollState = () => {
    const lists = {};
    for (const element of shell().querySelectorAll('*')) {
      if (element.scrollTop > 0 || element.scrollLeft > 0) lists[placeOf(element)] = [element.scrollLeft, element.scrollTop];
    }
    return { x: window.scrollX, y: window.scrollY, lists };
  };
  const restoreScroll = state => {
    if (!state) return;
    for (const [place, [left, top]] of Object.entries(state.lists || {})) {
      const element = elementAt(place);
      if (element) { element.scrollLeft = left; element.scrollTop = top; }
    }
    window.scrollTo(state.x || 0, state.y || 0);
  };

  // ---- Everything bound to the page's elements: run again after each update --------------------------------------------
  const bind = () => {
    // Sections
    const tabs = [...doc.querySelectorAll('[data-tab]')];
    const open = (id, remember) => {
      const tab = tabs.find(t => t.dataset.tab === id);
      if (!tab) return false;
      for (const t of tabs) {
        const on = t === tab;
        t.setAttribute('aria-selected', String(on));
        t.tabIndex = on ? 0 : -1;
      }
      const strip = tab.parentNode; // in a narrow window the tabs scroll sideways: keep the open one in the middle
      strip.scrollLeft = tab.offsetLeft - (strip.clientWidth - tab.offsetWidth) / 2;
      for (const pane of doc.querySelectorAll('.pane')) pane.hidden = pane.id !== 't-' + id;
      if (remember) memory.set('tab', id);
      return true;
    };
    const go = id => {
      if (!open(id, true)) return;
      window.scrollTo(0, 0);
      try { history.replaceState(null, '', '#' + id); } catch { /* file pages may refuse */ }
    };
    const fromAddress = location.hash.slice(1);
    if (!(fromAddress && open(fromAddress, true)) && !open(memory.get('tab') || '', false)) open(tabs[0].dataset.tab, false);
    tabs.forEach((tab, index) => {
      tab.addEventListener('click', () => go(tab.dataset.tab));
      tab.addEventListener('keydown', event => {
        const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key];
        if (!step) return;
        event.preventDefault();
        const next = tabs[(index + step + tabs.length) % tabs.length];
        go(next.dataset.tab);
        next.focus();
      });
    });
    for (const link of doc.querySelectorAll('[data-go]')) {
      link.addEventListener('click', event => { event.preventDefault(); go(link.dataset.go); });
    }

    // Treasure weeks
    const weeksButton = doc.querySelector('.weeks-btn'), weeks = doc.getElementById('weeks');
    if (weeksButton && weeks) {
      const showWeeks = visible => {
        weeks.hidden = !visible;
        weeksButton.setAttribute('aria-expanded', String(visible));
        weeksButton.textContent = weeksButton.dataset[visible ? 'close' : 'open'];
        memory.set('weeks', visible ? '1' : '0');
      };
      showWeeks(memory.get('weeks') === '1');
      weeksButton.addEventListener('click', () => showWeeks(weeks.hidden));
    }

    // Sortable tables (teammates)
    for (const table of doc.querySelectorAll('table.sort-table')) {
      const heads = [...table.tHead.rows[0].cells], rows = table.tBodies[0];
      const sortBy = (column, descending) => {
        const numeric = heads[column].dataset.type === 'n';
        const value = row => row.cells[column].dataset.v;
        const order = [...rows.rows].sort((a, b) => {
          const diff = numeric ? Number(value(a)) - Number(value(b)) : value(a).localeCompare(value(b));
          return descending ? -diff : diff;
        });
        order.forEach(row => rows.appendChild(row));
        heads.forEach((head, i) => i === column ? head.setAttribute('aria-sort', descending ? 'descending' : 'ascending') : head.removeAttribute('aria-sort'));
        memory.set('sort', column + (descending ? 'd' : 'a'));
      };
      heads.forEach((head, column) => head.querySelector('button').addEventListener('click', () => {
        const now = head.getAttribute('aria-sort'); // sorted: flip; not yet: numbers high-first, names A-Z
        sortBy(column, now ? now === 'ascending' : head.dataset.type === 'n');
      }));
      const saved = memory.get('sort');
      if (saved && heads[parseInt(saved, 10)]) sortBy(parseInt(saved, 10), saved.endsWith('d'));
    }

    // Weekly prizes: struck off by hand (obtained ones come struck off; the player's mark wins). Saved in the app's
    // settings when FCT serves the page; opened as a file they only show.
    for (const item of doc.querySelectorAll('.week li.mark')) {
      const flip = () => {
        if (!served || item.dataset.auto) return;
        const struck = !item.classList.contains('struck');
        // struck off = obtained: this week's card of the same prize turns gold at once (FCT rewrites the page too)
        const show = on => {
          item.classList.toggle('struck', on);
          item.setAttribute('aria-pressed', String(on));
          for (const card of doc.querySelectorAll('.prize[data-prize]')) {
            if (card.dataset.prize === item.dataset.prize) card.classList.toggle('prize-got', on);
          }
          const delta = on ? 1 : -1;
          const adjust = selector => {
            const el = doc.querySelector(selector);
            if (!el) return;
            const val = parseInt(el.textContent.replace(/\D/g, ''), 10);
            if (!isNaN(val)) el.textContent = Math.max(0, val + delta).toLocaleString();
          };
          adjust('[data-fig="tre-total"]');
          adjust('[data-fig="tre-featured"]');
          const tabCount = doc.querySelector('#tab-tesoros .tab-count');
          if (tabCount) {
            const count = parseInt(tabCount.textContent.replace(/\D/g, ''), 10);
            if (!isNaN(count)) {
              const next = Math.max(0, count + delta);
              tabCount.textContent = next > 0 ? next.toLocaleString() : '';
            }
          }
        };
        show(struck);
        post('mark', (struck ? '1' : '0') + '|' + item.dataset.prize).catch(() => { show(!struck); say(body.dataset.failed, true); });
      };
      item.addEventListener('click', flip);
      item.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); flip(); }
      });
    }

    // Settings: changed through FCT's local server
    const controls = [...doc.querySelectorAll('[data-key]')];
    if (!served) {
      controls.forEach(control => { control.disabled = true; });
      const note = doc.querySelector('.file-note');
      if (note) note.hidden = false;
    }
    const valueOf = control => control.type === 'checkbox' ? (control.checked ? '1' : '0') : control.value;
    for (const control of controls) {
      control.dataset.prev = valueOf(control);
      control.addEventListener('change', () => {
        const key = control.dataset.key, value = valueOf(control);
        saving = true;
        // A new language restarts FCT: wait until it answers again, then load the page in the new language.
        const reloadWhenBack = triesLeft => {
          ping().then(reply => { if (!reply.ok) throw new Error(); saving = false; location.reload(); })
            .catch(() => triesLeft > 0 ? setTimeout(() => reloadWhenBack(triesLeft - 1), 250) : (saving = false, location.reload()));
        };
        post(key, value)
          .then(() => {
            say(body.dataset.saved);
            if (key === 'lang') setTimeout(() => reloadWhenBack(16), 1000);
            else setTimeout(() => { saving = false; update(); }, 600); // the rewritten page, in place
          })
          .catch(() => {
            saving = false;
            say(body.dataset.failed, true);
            if (control.type === 'checkbox') control.checked = control.dataset.prev === '1';
            else control.value = control.dataset.prev;
          });
      });
    }
  };

  // ---- Fresh figures -----------------------------------------------------------------------------------------------------
  // Served by FCT: a quick ping tells the mark of the page FCT has now. Only when it differs from this one (the figures
  // or the settings really changed) is the new page fetched and swapped in place: no reload, no flash, and every list
  // keeps its scroll. A new language loads the page again (FCT restarts for it); FCT closing shows a notice.
  const showDown = on => { const down = doc.querySelector('.down'); if (down) down.hidden = !on; };
  const busy = () => {
    const focused = doc.activeElement;
    return saving || (focused && /^(INPUT|SELECT|TEXTAREA)$/.test(focused.tagName) && focused.closest('.set-sheet'));
  };
  let updating = false;
  const update = () => {
    if (updating || busy()) return;
    updating = true;
    fetch(location.pathname, { cache: 'no-store' })
      .then(reply => reply.ok ? reply.text() : Promise.reject())
      .then(text => {
        const fresh = new DOMParser().parseFromString(text, 'text/html');
        const next = fresh.querySelector('.shell');
        if (!next || busy()) return;
        const scroll = scrollState();
        // all in one go, before the browser paints: the new content never shows unbound or unscrolled
        shell().replaceWith(doc.adoptNode(next));
        for (const name of ['stamp', 'settings', 'refresh']) {
          if (fresh.body.dataset[name] !== undefined) body.dataset[name] = fresh.body.dataset[name];
        }
        bind();
        restoreScroll(scroll);
      })
      .catch(() => { /* next ping tries again */ })
      .finally(() => { updating = false; });
  };

  bind();
  let missed = 0;
  if (served) {
    const pageLanguage = doc.documentElement.lang || '';
    setInterval(() => {
      if (saving) return;
      ping().then(reply => reply.ok ? reply.json() : Promise.reject())
        .then(state => {
          missed = 0;
          showDown(false);
          if (state.lang && pageLanguage && state.lang !== pageLanguage) { location.reload(); return; }
          if (state.stamp && state.stamp !== body.dataset.stamp) update();
        })
        .catch(() => { if (++missed >= 3) showDown(true); });
    }, 1500);
  } else {
    // Opened as a file: nothing can tell when it changes, so it loads again every data-refresh seconds, keeping the
    // scroll of the window and of every list.
    restoreScroll(JSON.parse(memory.get('scroll') || 'null'));
    addEventListener('pagehide', () => memory.set('scroll', JSON.stringify(scrollState())));
    setInterval(() => { if (!busy()) location.reload(); }, (Number(body.dataset.refresh) || 15) * 1000);
  }
})();
