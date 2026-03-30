import { getMenu } from 'lib/medusa';
import { NextResponse } from 'next/server';

export const dynamic = 'force-dynamic';

export async function GET() {
  try {
    const menu = await getMenu('next-js-frontend-header-menu');
    return NextResponse.json(Array.isArray(menu) ? menu : []);
  } catch {
    return NextResponse.json([]);
  }
}
