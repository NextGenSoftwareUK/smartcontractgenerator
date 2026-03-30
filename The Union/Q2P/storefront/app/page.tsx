import HomePageClient from 'components/home-page-client';

export const revalidate = 43200; // 12 hours

export const metadata = {
  description: 'High-performance ecommerce store built with Next.js, Vercel, and Medusa.',
  openGraph: {
    type: 'website'
  }
};

/** Homepage renders only client content; data is loaded via /api/homepage to avoid Server Component errors. */
export default function HomePage() {
  return <HomePageClient />;
}
