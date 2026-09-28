function renderHandoff(markdown) {
  const root = document.createElement('div');
  root.className = 'handoff-content';
  const lines = (markdown || '').replace(/\r/g, '').split('\n');
  let section;
  let heading;
  let paragraph = [];
  let list;

  const ensureSection = () => {
    if (section) return section;
    section = document.createElement('section');
    section.className = 'handoff-section';
    root.append(section);
    return section;
  };

  const flushParagraph = () => {
    const text = paragraph.join('\n').trim();
    paragraph = [];
    if (!text) return;
    const target = ensureSection();
    if (text.startsWith('{') || text.startsWith('[')) {
      const details = document.createElement('details');
      details.className = 'handoff-record';
      const summary = document.createElement('summary');
      summary.textContent = 'Show structured record';
      const pre = document.createElement('pre');
      const code = document.createElement('code');
      try { code.textContent = JSON.stringify(JSON.parse(text), null, 2); }
      catch { code.textContent = text; }
      pre.append(code);
      details.append(summary, pre);
      target.append(details);
      return;
    }
    const p = document.createElement('p');
    p.textContent = text.replace(/\n+/g, ' ');
    target.append(p);
  };

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    const headingMatch = line.match(/^#{1,6}\s+(.+)$/);
    if (headingMatch) {
      flushParagraph();
      list = undefined;
      const nextHeading = headingMatch[1].trim();
      if (nextHeading.toLowerCase() === 'handoff' || nextHeading.toLowerCase() === heading?.toLowerCase()) continue;
      heading = nextHeading;
      section = document.createElement('section');
      section.className = 'handoff-section';
      const h3 = document.createElement('h3');
      h3.textContent = heading;
      section.append(h3);
      root.append(section);
      continue;
    }
    if (/^[-*]\s+/.test(line)) {
      flushParagraph();
      if (!list) {
        list = document.createElement('ul');
        ensureSection().append(list);
      }
      const item = document.createElement('li');
      item.textContent = line.replace(/^[-*]\s+/, '');
      list.append(item);
      continue;
    }
    if (!line.trim()) {
      flushParagraph();
      list = undefined;
      continue;
    }
    list = undefined;
    paragraph.push(line);
  }
  flushParagraph();
  return root;
}

fetch('/api/overview').then(r => r.json()).then(o => {
  const handoff = o.handoffs?.[0];
  const column = document.createElement('div');
  column.className = 'col-12';
  column.innerHTML = '<div class="card"><div class="card-header"><h2 class="card-title">Latest handoff</h2><span class="badge bg-azure-lt"></span></div><div class="card-body"></div></div>';
  column.querySelector('.badge').textContent = handoff?.id || 'None';
  const body = column.querySelector('.card-body');
  if (handoff) body.append(renderHandoff(handoff.markdown || handoff.summary));
  else {
    const empty = document.createElement('p');
    empty.className = 'mb-0';
    empty.textContent = 'No handoff has been recorded yet.';
    body.append(empty);
  }
  document.querySelector('#records')?.before(column);
}).catch(() => {});
