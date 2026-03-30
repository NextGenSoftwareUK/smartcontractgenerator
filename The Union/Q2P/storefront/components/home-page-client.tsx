'use client';

import { GridTileImage } from 'components/grid/tile';
import FooterMenu from 'components/layout/footer-menu';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import LogoSquare from 'components/logo-square';
import type { Product } from 'lib/medusa/types';
import type { Menu } from 'lib/medusa/types';

type HomeData = {
  gridProducts: Product[];
  carouselProducts: Product[];
  footerMenu: Menu[];
};

const SITE_NAME = 'The Union';

const EMPTY_GRID = (
  <section className="mx-auto max-w-screen-2xl px-4 pb-16 pt-16">
    <div className="rounded-xl border border-union-gold/20 bg-union-panel p-16 text-center">
      <div className="mb-6 flex justify-center">
        <span className="font-display text-6xl tracking-widest text-union-gold">THE UNION</span>
      </div>
      <p className="mb-3 font-display tracking-widest text-union-muted text-sm">
        PLAYA DEL CARMEN · EST. MEXICO
      </p>
      <div className="mx-auto mt-8 h-px w-24 bg-union-gold/40" />
      <p className="mt-8 text-sm text-union-muted">
        Gear dropping soon. Connect the store to see products.
      </p>
      <Link
        href="/search"
        className="mt-6 inline-block rounded border border-union-gold/40 px-6 py-2 font-display tracking-widest text-sm text-union-gold transition-colors hover:bg-union-gold hover:text-union-black"
      >
        BROWSE ALL GEAR
      </Link>
    </div>
  </section>
);

function ProductTile({
  product,
  size,
  priority
}: {
  product: Product;
  size: 'full' | 'half';
  priority?: boolean;
}) {
  const handle = product?.handle ?? '';
  const featuredImage = product?.featuredImage;
  const priceRange = product?.priceRange?.maxVariantPrice;
  const src = featuredImage?.url && String(featuredImage.url).trim() ? featuredImage.url : undefined;
  const amount = priceRange?.amount != null ? String(priceRange.amount) : '0';
  const currencyCode = priceRange?.currencyCode && String(priceRange.currencyCode).trim() ? priceRange.currencyCode : 'USD';
  const title = product?.title != null ? String(product.title) : '';
  return (
    <div className={size === 'full' ? 'md:col-span-4 md:row-span-2' : 'md:col-span-2 md:row-span-1'}>
      <Link className="relative block aspect-square h-full w-full" href={handle ? `/product/${handle}` : '#'}>
        <GridTileImage
          src={src}
          fill
          sizes={size === 'full' ? '(min-width: 768px) 66vw, 100vw' : '(min-width: 768px) 33vw, 100vw'}
          priority={priority}
          alt={title}
          label={{ position: size === 'full' ? 'center' : 'bottom', title, amount, currencyCode }}
        />
      </Link>
    </div>
  );
}

export default function HomePageClient() {
  const [data, setData] = useState<HomeData | null>(null);

  useEffect(() => {
    fetch('/api/homepage')
      .then((r) => r.json())
      .then((d) => setData(d))
      .catch(() => setData({ gridProducts: [], carouselProducts: [], footerMenu: [] }));
  }, []);

  if (data === null) {
    return (
      <section className="mx-auto max-w-screen-2xl px-4 pb-16 pt-16">
        <div className="rounded-xl border border-union-gold/20 bg-union-panel p-16 text-center">
          <div className="mx-auto h-1 w-16 animate-pulse rounded bg-union-gold/40" />
          <p className="mt-6 font-display tracking-widest text-union-muted text-sm">LOADING GEAR…</p>
        </div>
      </section>
    );
  }

  const { gridProducts, carouselProducts, footerMenu } = data;
  const grid = Array.isArray(gridProducts) ? gridProducts : [];
  const carousel = Array.isArray(carouselProducts) ? carouselProducts : [];
  const menu = Array.isArray(footerMenu) ? footerMenu : [];
  const [first, second, third] = grid;

  const currentYear = new Date().getFullYear();

  return (
    <>
      {/* Hero banner */}
      <div className="border-b border-union-gold/20 bg-union-panel px-4 py-6 text-center">
        <p className="font-display tracking-[0.3em] text-union-muted text-xs">
          PLAYA DEL CARMEN &nbsp;·&nbsp; OFFICIAL STORE &nbsp;·&nbsp; EST. MEXICO
        </p>
      </div>

      {first && second && third ? (
        <section className="mx-auto grid max-w-screen-2xl gap-4 px-4 pb-4 pt-4 md:grid-cols-6 md:grid-rows-2">
          <ProductTile product={first} size="full" priority />
          <ProductTile product={second} size="half" priority />
          <ProductTile product={third} size="half" />
        </section>
      ) : (
        EMPTY_GRID
      )}

      {carousel.length > 0 && (
        <div className="w-full overflow-x-auto pb-6 pt-1">
          <ul className="flex animate-carousel gap-4">
            {[...carousel, ...carousel, ...carousel].map((product, i) => {
              const handle = product?.handle ?? '';
              const featuredImage = product?.featuredImage;
              const priceRange = product?.priceRange?.maxVariantPrice;
              const src = featuredImage?.url && String(featuredImage.url).trim() ? featuredImage.url : undefined;
              const amount = priceRange?.amount != null ? String(priceRange.amount) : '0';
              const currencyCode = priceRange?.currencyCode && String(priceRange.currencyCode).trim() ? priceRange.currencyCode : 'USD';
              const title = product?.title != null ? String(product.title) : '';
              return (
                <li
                  key={`${handle || i}-${i}`}
                  className="relative aspect-square h-[30vh] max-h-[275px] w-2/3 max-w-[475px] flex-none md:w-1/3"
                >
                  <Link href={handle ? `/product/${handle}` : '#'} className="relative h-full w-full">
                    <GridTileImage
                      alt={featuredImage?.altText ?? title}
                      label={{ title, amount, currencyCode }}
                      src={src}
                      fill
                      sizes="(min-width: 1024px) 25vw, (min-width: 768px) 33vw, 50vw"
                    />
                  </Link>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      {/* Union info strip */}
      <div className="border-y border-union-gold/20 bg-union-panel py-8 px-4">
        <div className="mx-auto flex max-w-4xl flex-col items-center gap-2 text-center md:flex-row md:justify-between md:text-left">
          <div>
            <p className="font-display tracking-widest text-union-gold text-sm">EARN WHILE YOU TRAIN</p>
            <p className="mt-1 text-xs text-union-muted">Complete quests. Unlock discounts. Rep the Union.</p>
          </div>
          <div className="h-px w-full bg-union-gold/20 md:hidden" />
          <div>
            <p className="font-display tracking-widest text-union-gold text-sm">CHAPTER NETWORK</p>
            <p className="mt-1 text-xs text-union-muted">PDC · Tulum · Coming worldwide</p>
          </div>
          <div className="h-px w-full bg-union-gold/20 md:hidden" />
          <div>
            <p className="font-display tracking-widest text-union-gold text-sm">FIGHTER PASSPORT</p>
            <p className="mt-1 text-xs text-union-muted">Your training history. Verified. Portable.</p>
          </div>
        </div>
      </div>

      <footer className="border-t border-union-gold/20 bg-union-panel text-union-muted">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-6 py-12 text-sm md:flex-row md:gap-12 md:px-4 xl:px-0">
          <div>
            <Link className="flex items-center gap-3 md:pt-1" href="/">
              <LogoSquare size="sm" />
              <span className="font-display tracking-[0.18em] text-union-gold">{SITE_NAME}</span>
            </Link>
            <p className="mt-3 max-w-xs text-xs leading-relaxed text-union-muted">
              Playa del Carmen&apos;s original fight gym. Train hard. Represent.
            </p>
          </div>
          <FooterMenu menu={menu.map((m) => ({ title: m?.title ?? '', path: m?.path ?? '#' }))} />
          <div className="md:ml-auto">
            <a
              href="https://hh-pdc-wsyc.vercel.app/playa-map.html"
              target="_blank"
              rel="noopener noreferrer"
              className="rounded border border-union-gold/30 px-4 py-1.5 font-display tracking-widest text-xs text-union-gold transition-colors hover:bg-union-gold hover:text-union-black"
            >
              FIND US ON THE MAP →
            </a>
          </div>
        </div>
        <div className="border-t border-union-gold/10 py-5 text-xs">
          <div className="mx-auto flex w-full max-w-7xl flex-col items-center gap-1 px-4 md:flex-row md:gap-0">
            <p className="text-union-muted">
              &copy; {currentYear} {SITE_NAME}. All rights reserved. Playa del Carmen, Mexico.
            </p>
            <p className="md:ml-auto text-union-muted/50">
              Built on OASIS · Powered by Medusa
            </p>
          </div>
        </div>
      </footer>
    </>
  );
}
