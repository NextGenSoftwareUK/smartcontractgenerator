import clsx from 'clsx';
import LogoIcon from './icons/logo';

export default function LogoSquare({ size }: { size?: 'sm' | undefined }) {
  return (
    <div
      className={clsx(
        'flex flex-none items-center justify-center border border-union-gold/30 bg-union-panel text-union-gold',
        {
          'h-[40px] w-[40px] rounded-lg': !size,
          'h-[30px] w-[30px] rounded-md': size === 'sm'
        }
      )}
    >
      <LogoIcon
        className={clsx({
          'h-[22px] w-[22px]': !size,
          'h-[16px] w-[16px]': size === 'sm'
        })}
      />
    </div>
  );
}
