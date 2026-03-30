import Link from 'next/link';
import FooterMenu from 'components/layout/footer-menu';
import LogoSquare from 'components/logo-square';
import { getMenu } from 'lib/medusa';
import type { Menu } from 'lib/medusa/types';

const SITE_NAME = process.env.SITE_NAME ?? 'The Union';

export default async function Footer({ initialMenu }: { initialMenu?: Menu[] } = {}) {
  const currentYear = new Date().getFullYear();
  let menu: Menu[] = Array.isArray(initialMenu)
    ? initialMenu.map((m) => ({ title: m?.title ?? '', path: m?.path ?? '#' }))
    : [];
  if (!menu.length) {
    try {
      const fetched = await getMenu('next-js-frontend-footer-menu');
      menu = Array.isArray(fetched)
        ? fetched.map((m: { title?: string; path?: string }) => ({
            title: m?.title ?? '',
            path: m?.path ?? '#'
          }))
        : [];
    } catch {
      menu = [];
    }
  }

  return (
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
        <FooterMenu menu={menu} />
        <div className="md:ml-auto flex flex-col items-start gap-3">
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
  );
}
