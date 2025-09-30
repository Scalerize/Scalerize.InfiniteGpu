import { useLayoutEffect, type ReactNode } from 'react';
import { useThemeStore } from '../stores/themeStore';

interface ThemeProviderProps {
  children: ReactNode;
}

const applyTheme = (isDark: boolean) => {
  const root = window.document.documentElement;
  root.classList.remove('light', 'dark');
  root.classList.add(isDark ? 'dark' : 'light');
};

export const ThemeProvider = ({ children }: ThemeProviderProps) => {
  const mode = useThemeStore((state) => state.mode);

  // Use useLayoutEffect to apply theme synchronously before paint
  useLayoutEffect(() => {
    if (mode === 'system') {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      applyTheme(mediaQuery.matches);

      const listener = (e: MediaQueryListEvent) => {
        applyTheme(e.matches);
      };

      mediaQuery.addEventListener('change', listener);
      
      return () => {
        mediaQuery.removeEventListener('change', listener);
      };
    } else {
      applyTheme(mode === 'dark');
    }
  }, [mode]);

  return <>{children}</>;
};