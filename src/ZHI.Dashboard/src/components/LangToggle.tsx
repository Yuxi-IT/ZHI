import { useT } from '../i18n/I18nContext'

export function LangToggle() {
  const { lang, setLang } = useT()

  return (
    <button
      onClick={() => setLang(lang === 'zh' ? 'en' : 'zh')}
      className="px-1.5 py-0.5 text-[9px] rounded border border-neutral-800 text-neutral-500 hover:text-neutral-300 hover:border-neutral-600 transition-colors"
      title={lang === 'zh' ? 'Switch to English' : '切换到中文'}
    >
      {lang === 'zh' ? 'EN' : '中'}
    </button>
  )
}
