import Cart from 'components/cart';
import OpenCart from 'components/cart/open-cart';
import LogoSquare from 'components/logo-square';
import { getMenu } from 'lib/medusa';
import { Menu } from 'lib/medusa/types';
import Link from 'next/link';
import { Suspense } from 'react';
import MobileMenu from './mobile-menu';
import Search from './search';

const SITE_NAME = process.env.SITE_NAME ?? 'The Union';

export default async function Navbar() {
  let menu: Awaited<ReturnType<typeof getMenu>> = [];
  try {
    menu = await getMenu('next-js-frontend-header-menu');
  } catch {
    menu = [];
  }
  if (!Array.isArray(menu)) menu = [];

  return (
    <nav className="relative flex items-center justify-between border-b border-union-gold/20 bg-union-black/95 px-4 py-3 backdrop-blur-sm lg:px-6">
      <div className="block flex-none md:hidden">
        <MobileMenu menu={menu} />
      </div>
      <div className="flex w-full items-center">
        <div className="flex w-full md:w-1/3">
          <Link
            href="/"
            aria-label="Go back home"
            className="mr-2 flex w-full items-center justify-center gap-3 md:w-auto lg:mr-6"
          >
            <LogoSquare />
            <div className="flex-none font-display text-xl tracking-[0.15em] text-union-gold md:hidden lg:block">
              {SITE_NAME}
            </div>
          </Link>
          {menu.length ? (
            <ul className="hidden gap-6 text-sm md:flex md:items-center">
              {menu.map((item: Menu) => (
                <li key={item.title}>
                  <Link
                    href={item.path}
                    className="font-display tracking-widest text-union-muted transition-colors hover:text-union-gold"
                  >
                    {item.title.toUpperCase()}
                  </Link>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
        <div className="hidden justify-center md:flex md:w-1/3">
          <Search />
        </div>
        <div className="flex justify-end md:w-1/3">
          <Suspense fallback={<OpenCart />}>
            <Cart />
          </Suspense>
        </div>
      </div>
    </nav>
  );
}
