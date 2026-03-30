import Grid from 'components/grid';
import { GridTileImage } from 'components/grid/tile';
import { Product } from 'lib/medusa/types';
import Link from 'next/link';

export default function ProductGridItems({ products }: { products: Product[] }) {
  const list = Array.isArray(products) ? products : [];
  return (
    <>
      {list.map((product) => {
        const handle = product?.handle ?? '';
        const priceRange = product?.priceRange?.maxVariantPrice;
        const amount = priceRange?.amount != null ? String(priceRange.amount) : '0';
        const currencyCode = priceRange?.currencyCode && String(priceRange.currencyCode).trim() ? priceRange.currencyCode : 'USD';
        const title = product?.title != null ? String(product.title) : '';
        const src = product?.featuredImage?.url && String(product.featuredImage.url).trim() ? product.featuredImage.url : undefined;
        return (
        <Grid.Item key={handle || title} className="animate-fadeIn">
          <Link className="relative inline-block h-full w-full" href={handle ? `/product/${handle}` : '#'}>
            <GridTileImage
              alt={title}
              label={{
                title,
                amount,
                currencyCode
              }}
              src={src}
              fill
              sizes="(min-width: 768px) 33vw, (min-width: 640px) 50vw, 100vw"
            />
          </Link>
        </Grid.Item>
        );
      })}
    </>
  );
}
