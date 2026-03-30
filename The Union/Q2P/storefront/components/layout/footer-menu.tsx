'use client';

import clsx from 'clsx';
import { Menu } from 'lib/medusa/types';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useState } from 'react';

const FooterMenuItem = ({ item }: { item: Menu }) => {
  const pathname = usePathname();
  const [active, setActive] = useState(pathname === item.path);

  useEffect(() => {
    setActive(pathname === item.path);
  }, [pathname, item.path]);

  return (
    <li>
      <Link
        href={item.path}
        className={clsx(
          'block p-2 text-lg underline-offset-4 hover:text-black hover:underline dark:hover:text-neutral-300 md:inline-block md:text-sm',
          {
            'text-black dark:text-neutral-300': active
          }
        )}
      >
        {item.title}
      </Link>
    </li>
  );
};

export default function FooterMenu({ menu }: { menu: Menu[] }) {
  const list = Array.isArray(menu) ? menu.filter((item) => item && (item.path != null || item.title != null)) : [];
  if (!list.length) return null;

  return (
    <nav>
      <ul>
        {list.map((item: Menu, i) => (
          <FooterMenuItem key={item?.title ?? item?.path ?? i} item={{ title: item?.title ?? '', path: item?.path ?? '#' }} />
        ))}
      </ul>
    </nav>
  );
}
