import { getAuthToken } from '../../features/auth/stores/authStore';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

interface ApiRequestOptions<TBody> {
  method?: HttpMethod;
  body?: TBody;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  authenticated?: boolean;
}

const resolveBaseUrl = () => {
  const configured = import.meta.env.VITE_BACKEND_URL;
  if (configured && configured.length > 0) {
    return configured.replace(/\/+$/, '');
  }

  // Vite dev server proxy can forward /api to backend
  return '';
};

export const API_BASE_URL = resolveBaseUrl();


export async function apiRequest<TResponse, TBody = unknown>(
  path: string,
  options: ApiRequestOptions<TBody> = {}
): Promise<TResponse> {
  const {
    method = 'GET',
    body,
    headers = {},
    signal,
    authenticated = true
  } = options;

  const finalHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
    ...headers
  };

  if (!authenticated) {
    delete finalHeaders['Authorization'];
  }

  if (authenticated) {
    const token = getAuthToken();
    if (token) {
      finalHeaders['Authorization'] = `Bearer ${token}`;
    }
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: finalHeaders,
    body: body != null ? JSON.stringify(body) : undefined,
    signal
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new Error(`API ${method} ${path} failed: ${response.status} ${errorText}`);
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return response.json() as Promise<TResponse>;
}