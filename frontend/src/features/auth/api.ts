import { apiRequest } from '../../shared/utils/apiClient';

interface AuthTokenResponse {
  token?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
}

const ensureToken = (context: 'login' | 'register', response: AuthTokenResponse) => {
  if (typeof response.token !== 'string' || response.token.length === 0) {
    throw new Error(`Auth ${context} response did not include a token.`);
  }

  return response.token;
};

export const login = async ({ email, password }: LoginRequest): Promise<string> => {
  const response = await apiRequest<AuthTokenResponse, LoginRequest>('/api/auth/login', {
    method: 'POST',
    body: { email, password },
    authenticated: false
  });

  return ensureToken('login', response);
};

export const register = async (payload: RegisterRequest): Promise<string> => {
  const response = await apiRequest<AuthTokenResponse, RegisterRequest>('/api/auth/register', {
    method: 'POST',
    body: payload,
    authenticated: false
  });

  return ensureToken('register', response);
};