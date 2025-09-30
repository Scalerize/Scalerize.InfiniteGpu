export interface DecodedJwtPayload {
  sub?: string;
  email?: string;
  role?: string;
  [key: string]: unknown;
}

const padBase64 = (value: string) => {
  const padding = (4 - (value.length % 4)) % 4;
  return value.padEnd(value.length + padding, '=');
};

export const parseJwt = (token: string): DecodedJwtPayload | null => {
  try {
    const [, payloadSegment] = token.split('.');
    if (!payloadSegment) {
      return null;
    }

    const normalized = padBase64(payloadSegment.replace(/-/g, '+').replace(/_/g, '/'));
    const decoded = atob(normalized);
    const json = decodeURIComponent(
      decoded
        .split('')
        .map((char) => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join('')
    );

    return JSON.parse(json) as DecodedJwtPayload;
  } catch {
    return null;
  }
};