import clsx from 'clsx';

const VALID_CURRENCY = /^[A-Z]{3}$/;
const normalizeCurrency = (code: string) =>
  (code && VALID_CURRENCY.test(String(code).trim()) ? String(code).trim() : 'USD');

const Price = ({
  amount,
  className,
  currencyCode = 'USD',
  currencyCodeClassName
}: {
  amount: string;
  className?: string;
  currencyCode: string;
  currencyCodeClassName?: string;
} & React.ComponentProps<'p'>) => {
  const safeCurrency = normalizeCurrency(currencyCode);
  const numericAmount = Number(amount);
  const safeAmount = Number.isFinite(numericAmount) ? numericAmount : 0;
  return (
  <p suppressHydrationWarning={true} className={className}>
    {`${new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: safeCurrency,
      currencyDisplay: 'narrowSymbol'
    }).format(safeAmount)}`}
    <span className={clsx('ml-1 inline', currencyCodeClassName)}>{`${safeCurrency}`}</span>
  </p>
  );
};

export default Price;
