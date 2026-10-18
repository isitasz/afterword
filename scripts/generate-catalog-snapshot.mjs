import { createHash } from 'node:crypto';
import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outputDirectory = path.join(projectRoot, 'wwwroot', 'catalog-data');
const coversDirectory = path.join(outputDirectory, 'covers');
const apiOriginValue = process.env.AFTERWORD_API_ORIGIN?.trim();

if (!apiOriginValue) {
  throw new Error('Set AFTERWORD_API_ORIGIN to the public HTTPS URL of the Afterword API.');
}

const apiOrigin = new URL(apiOriginValue);
if (apiOrigin.protocol !== 'https:' || apiOrigin.username || apiOrigin.password || apiOrigin.search || apiOrigin.hash) {
  throw new Error('AFTERWORD_API_ORIGIN must be a public HTTPS origin without credentials, query, or fragment.');
}
apiOrigin.pathname = apiOrigin.pathname.replace(/\/+$/, '');

const catalogs = ['books', 'games', 'movies', 'animes'];

async function fetchJson(route) {
  const response = await fetch(new URL(route, apiOrigin), { signal: AbortSignal.timeout(120_000) });
  if (!response.ok) {
    throw new Error(`The API returned ${response.status} for ${route}; keeping the previous Vercel deployment.`);
  }

  const value = await response.json();
  if (!Array.isArray(value)) {
    throw new Error(`The API returned an invalid catalog for ${route}; keeping the previous Vercel deployment.`);
  }
  return value;
}

async function mapWithConcurrency(items, concurrency, action) {
  let nextIndex = 0;
  const workers = Array.from({ length: Math.min(concurrency, items.length) }, async () => {
    while (nextIndex < items.length) {
      const index = nextIndex++;
      await action(items[index]);
    }
  });
  await Promise.all(workers);
}

function coverExtension(contentType) {
  const mimeType = contentType.split(';', 1)[0].trim().toLowerCase();
  const extensions = new Map([
    ['image/avif', '.avif'],
    ['image/bmp', '.bmp'],
    ['image/gif', '.gif'],
    ['image/jpg', '.jpg'],
    ['image/jpeg', '.jpg'],
    ['image/png', '.png'],
    ['image/svg+xml', '.svg'],
    ['image/webp', '.webp'],
  ]);
  const extension = extensions.get(mimeType);
  if (!extension) {
    throw new Error(`A book cover has an unsupported content type (${mimeType || 'missing'}).`);
  }
  return extension;
}

await rm(coversDirectory, { recursive: true, force: true });
await mkdir(coversDirectory, { recursive: true });

for (const kind of catalogs) {
  const entries = await fetchJson(`/api/${kind}`);

  if (kind === 'books') {
    await mapWithConcurrency(entries, 6, async book => {
      if (!book.cover) return;

      const coverUrl = new URL(book.cover, apiOrigin);
      if (coverUrl.origin !== apiOrigin.origin ||
          !coverUrl.pathname.startsWith('/api/books/') ||
          !coverUrl.pathname.endsWith('/cover')) {
        throw new Error(`The API returned an unexpected cover URL for book ${book.id}.`);
      }

      const response = await fetch(coverUrl, { signal: AbortSignal.timeout(120_000) });
      if (!response.ok) {
        throw new Error(`Could not download the stored cover for book ${book.id} (HTTP ${response.status}).`);
      }

      const contentType = response.headers.get('content-type') || '';
      const extension = coverExtension(contentType);
      const bytes = Buffer.from(await response.arrayBuffer());
      if (bytes.length === 0) {
        throw new Error(`The stored cover for book ${book.id} is empty.`);
      }

      const hash = createHash('sha256').update(bytes).digest('hex').slice(0, 24);
      const fileName = `${hash}${extension}`;
      await writeFile(path.join(coversDirectory, fileName), bytes);
      book.cover = `/catalog-data/covers/${fileName}`;
    });
  }

  await writeFile(
    path.join(outputDirectory, `${kind}.json`),
    `${JSON.stringify(entries)}\n`,
    'utf8'
  );
  console.log(`Generated ${kind}.json (${entries.length} entries).`);
}
