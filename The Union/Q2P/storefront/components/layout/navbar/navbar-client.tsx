'use client';

import CartModal from 'components/cart/modal';
import OpenCart from 'components/cart/open-cart';
import LogoSquare from 'components/logo-square';
import type { Cart } from 'lib/medusa/types';
import type { Menu } from 'lib/medusa/types';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import MobileMenu from './mobile-menu';
import Search from './search';

const SITE_NAME = process.env.NEXT_PUBLIC_SITE_NAME ?? 'Store';

export default function NavbarClient() {
  const [menu, setMenu] = useState<Menu[]>([]);
  const [cart, setCart] = useState<Cart | null | undefined>(undefined);

  useEffect(() => {
    Promise.all([
      fetch('/api/header-menu').then((r) => r.json()),
      fetch('/api/cart', { credentials: 'include' }).then((r) => r.json())
    ])
      .then(([menuData, cartData]) => {
        setMenu(Array.isArray(menuData) ? menuData : []);
        setCart(cartData?.cart ?? null);
      })
      .catch(() => {
        setMenu([]);
        setCart(null);
      });
  }, []);

  const menuList = Array.isArray(menu) ? menu : [];

  return (
    <nav className="relative flex items-center justify-between p-4 lg:px-6">
      <div className="block flex-none md:hidden">
        <MobileMenu menu={menuList} />
      </div>
      <div className="flex w-full items-center">
        <div className="flex w-full md:w-1/3">
          <Link
            href="/"
            aria-label="Go back home"
            className="mr-2 flex w-full items-center justify-center md:w-auto lg:mr-6"
          >
            <LogoSquare />
            <div className="ml-2 flex-none text-sm font-medium uppercase md:hidden lg:block">
              {SITE_NAME}
            </div>
          </Link>
          {menuList.length > 0 ? (
            <ul className="hidden gap-6 text-sm md:flex md:items-center">
              {menuList.map((item: Menu) => (
                <li key={item.title}>
                  <Link
                    href={item.path ?? '#'}
                    className="text-neutral-500 underline-offset-4 hover:text-black hover:underline dark:text-neutral-400 dark:hover:text-neutral-300"
                  >
                    {item.title}
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
          {cart !== undefined ? (
            <CartModal cart={cart ?? undefined} />
          ) : (
            <OpenCart />
          )}
        </div>
      </div>
    </nav>
  );
}
