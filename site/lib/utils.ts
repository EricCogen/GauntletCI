import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function addUtmParams(href: string, source: string, medium: string, campaign: string): string {
  if (href.startsWith('http') || href.startsWith('mailto:')) {
    return href;
  }
  
  const separator = href.includes('?') ? '&' : '?';
  return `${href}${separator}utm_source=${source}&utm_medium=${medium}&utm_campaign=${campaign}`;
}
