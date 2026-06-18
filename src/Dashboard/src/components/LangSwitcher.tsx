import { Button } from '@heroui/react';
import { useT } from '../i18n/I18nContext';

export function LangSwitcher() {
  const { lang, setLang } = useT();

  return (
    <Button
      isIconOnly
      variant="ghost"
      size="sm"
      className="min-w-0 w-6 h-6 text-[10px] font-semibold text-foreground-500 hover:text-foreground"
      aria-label="Switch language"
      onPress={() => setLang(lang === 'zh' ? 'en' : 'zh')}
    >
      {lang === 'zh' ? 'EN' : '中'}
    </Button>
  );
}
