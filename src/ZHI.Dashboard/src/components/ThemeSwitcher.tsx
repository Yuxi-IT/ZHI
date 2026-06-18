import { Button, useTheme } from '@heroui/react';
import { Sun, Moon } from '@gravity-ui/icons';

export function ThemeSwitcher() {
  const { resolvedTheme, setTheme } = useTheme('system');

  return (
    <Button
      isIconOnly
      variant="ghost"
      size="sm"
      className="min-w-0 w-6 h-6 text-foreground-500 hover:text-foreground"
      aria-label="Toggle theme"
      onPress={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
    >
      {resolvedTheme === 'dark' ? <Sun className="size-3.5" /> : <Moon className="size-3.5" />}
    </Button>
  );
}
