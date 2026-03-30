import { getCategoryProducts, getMenu, getProducts } from 'lib/medusa';
import { NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

export async function GET() {
  try {
    const [gridResult, carouselResult, footerMenuResult] = await Promise.allSettled([
      (async () => {
        let p = await getCategoryProducts('hidden-homepage-featured-items');
        if (!p?.[0] || !p?.[1] || !p?.[2]) {
          const all = await getProducts({});
          p = (all ?? []).slice(0, 3);
        }
        return p ?? [];
      })(),
      (async () => {
        let p = await getCategoryProducts('hidden-homepage-carousel');
        if (!p?.length) p = await getProducts({});
        return p ?? [];
      })(),
      getMenu('next-js-frontend-footer-menu')
    ]);

    const gridProducts = gridResult.status === 'fulfilled' ? gridResult.value : [];
    const carouselProducts = carouselResult.status === 'fulfilled' ? carouselResult.value : [];
    const footerMenu = footerMenuResult.status === 'fulfilled' ? footerMenuResult.value : [];

    return NextResponse.json({
      gridProducts: Array.isArray(gridProducts) ? gridProducts : [],
      carouselProducts: Array.isArray(carouselProducts) ? carouselProducts : [],
      footerMenu: Array.isArray(footerMenu) ? footerMenu : []
    });
  } catch {
    return NextResponse.json({
      gridProducts: [],
      carouselProducts: [],
      footerMenu: []
    });
  }
}
