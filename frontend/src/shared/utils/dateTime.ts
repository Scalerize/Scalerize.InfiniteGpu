/**
 * Date and time utility functions for timezone conversion
 */

export type FormatUtcToLocalOptions = Intl.DateTimeFormatOptions & {
  /**
   * Whether to include the timezone abbreviation or offset in the formatted result.
   * Defaults to false to keep the rendered value as a pure local date/time string.
   */
  includeTimeZoneName?: boolean;
};

/**
 * Normalizes incoming UTC-like values to ensure we always interpret them as UTC
 * even if the incoming string lacks a timezone designator.
 *
 * Browsers will assume "local time" when the timezone designator is missing, which
 * would break consumers expecting UTC inputs. By appending a "Z" we force UTC parsing.
 */
function normalizeUtcInput(utcDate: Date | string | number): Date {
  if (utcDate instanceof Date) {
    // clone to avoid accidental external mutation
    return new Date(utcDate.getTime());
  }

  if (typeof utcDate === "string") {
    const trimmed = utcDate.trim();
    const hasTimeDesignator = /[zZ]|[+-]\d{2}:?\d{2}$/.test(trimmed);

    if (hasTimeDesignator) {
      return new Date(trimmed);
    }

    // If the string lacks a timezone, assume it was provided in UTC.
    return new Date(`${trimmed}Z`);
  }

  return new Date(utcDate);
}

/**
 * Converts a UTC date to the user's local timezone
 * Handles daylight saving time automatically
 *
 * @param utcDate - UTC date as Date object, ISO string, or timestamp
 * @returns Date object in local timezone
 *
 * @example
 * // UTC: 29 Sept 17:38
 * // Local (France, UTC+2): 29 Sept 19:38
 * const utcDate = new Date('2025-09-29T17:38:00Z');
 * const localDate = utcToLocalTime(utcDate);
 */
export function utcToLocalTime(utcDate: Date | string | number): Date {
  const date = normalizeUtcInput(utcDate);

  // Date objects in JavaScript automatically handle timezone conversion
  // When you create a Date from UTC and access it, it uses local timezone
  return date;
}

/**
 * Formats a UTC date to local timezone with custom format
 * 
 * @param utcDate - UTC date as Date object, ISO string, or timestamp
 * @param options - Intl.DateTimeFormat options for formatting
 * @returns Formatted string in local timezone
 * 
 * @example
 * const utcDate = '2025-09-29T17:38:00Z';
 * formatUtcToLocal(utcDate, { 
 *   day: 'numeric', 
 *   month: 'short', 
 *   hour: '2-digit', 
 *   minute: '2-digit' 
 * }); // "29 Sept 19:38" (in France)
 */
export function formatUtcToLocal(
  utcDate: Date | string | number,
  options?: FormatUtcToLocalOptions
): string {
  const date = normalizeUtcInput(utcDate);
  const {
    includeTimeZoneName = false,
    timeZoneName,
    ...restOptions
  } = options ?? {};
  
  const defaultOptions: Intl.DateTimeFormatOptions = {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  };

  const finalOptions: Intl.DateTimeFormatOptions = {
    ...defaultOptions,
    ...restOptions,
  };

  if (includeTimeZoneName) {
    finalOptions.timeZoneName = timeZoneName ?? 'short';
  }

  return new Intl.DateTimeFormat(undefined, finalOptions).format(date);
}

/**
 * Formats a UTC date to local timezone with full date and time
 * 
 * @param utcDate - UTC date as Date object, ISO string, or timestamp
 * @returns Formatted string like "29 September 2025, 19:38"
 */
export function formatUtcToLocalFull(utcDate: Date | string | number): string {
  const date = normalizeUtcInput(utcDate);
  
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);
}

/**
 * Formats a UTC date to local timezone with short format
 * 
 * @param utcDate - UTC date as Date object, ISO string, or timestamp
 * @returns Formatted string like "29/09/25 19:38"
 */
export function formatUtcToLocalShort(utcDate: Date | string | number): string {
  const date = normalizeUtcInput(utcDate);
  
  return new Intl.DateTimeFormat(undefined, {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(date);
}

/**
 * Gets the user's current timezone offset in hours
 * 
 * @returns Timezone offset as a string (e.g., "UTC+2", "UTC-5")
 * 
 * @example
 * getTimezoneOffset(); // "UTC+2" (in France during summer)
 */
export function getTimezoneOffset(): string {
  const offset = -new Date().getTimezoneOffset() / 60;
  const sign = offset >= 0 ? '+' : '-';
  return `UTC${sign}${Math.abs(offset)}`;
}

/**
 * Gets the user's timezone name
 * 
 * @returns Timezone name (e.g., "Europe/Paris")
 */
export function getTimezoneName(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

/**
 * Checks if daylight saving time is currently active
 * 
 * @returns true if DST is active, false otherwise
 */
export function isDaylightSavingTime(date?: Date): boolean {
  const checkDate = date || new Date();
  const jan = new Date(checkDate.getFullYear(), 0, 1);
  const jul = new Date(checkDate.getFullYear(), 6, 1);
  const stdTimezoneOffset = Math.max(jan.getTimezoneOffset(), jul.getTimezoneOffset());
  
  return checkDate.getTimezoneOffset() < stdTimezoneOffset;
}

/**
 * Converts UTC date to local time and returns both Date object and formatted string
 * 
 * @param utcDate - UTC date as Date object, ISO string, or timestamp
 * @returns Object with local Date and formatted string
 * 
 * @example
 * const { date, formatted } = getLocalDateTime('2025-09-29T17:38:00Z');
 * console.log(formatted); // "29 Sept 19:38"
 */
export function getLocalDateTime(utcDate: Date | string | number): {
  date: Date;
  formatted: string;
  fullFormatted: string;
  shortFormatted: string;
  timezone: string;
  timezoneName: string;
} {
  const date = normalizeUtcInput(utcDate);
  
  return {
    date,
    formatted: formatUtcToLocal(date),
    fullFormatted: formatUtcToLocalFull(date),
    shortFormatted: formatUtcToLocalShort(date),
    timezone: getTimezoneOffset(),
    timezoneName: getTimezoneName(),
  };
}

/**
 * Converts a local date to UTC
 * 
 * @param localDate - Local date as Date object
 * @returns UTC date as ISO string
 */
export function localToUtc(localDate: Date): string {
  return localDate.toISOString();
}

/**
 * Gets relative time string (e.g., "2 hours ago", "in 3 days")
 * 
 * @param utcDate - UTC date as Date object, ISO string, or timestamp
 * @returns Relative time string
 */
export function getRelativeTime(utcDate: Date | string | number): string {
  const date = normalizeUtcInput(utcDate);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSeconds = Math.floor(diffMs / 1000);
  const diffMinutes = Math.floor(diffSeconds / 60);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);
  
  if (diffSeconds < 60) {
    return 'just now';
  } else if (diffMinutes < 60) {
    return `${diffMinutes} minute${diffMinutes !== 1 ? 's' : ''} ago`;
  } else if (diffHours < 24) {
    return `${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`;
  } else if (diffDays < 30) {
    return `${diffDays} day${diffDays !== 1 ? 's' : ''} ago`;
  } else {
    return formatUtcToLocal(date);
  }
}