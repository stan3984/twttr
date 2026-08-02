export interface TIdentity {
  id: string;
  username: string;
  displayName: string;
}

export interface TLoginRequest {
  username: string;
  password: string;
}

export interface TRegisterRequest {
  username: string;
  password: string;
  email: string;
}

export interface TProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}

// Mirrored from AuthController.
export const USERNAME_MIN = 8;
export const USERNAME_MAX = 24;
export const PASSWORD_MIN = 12;
export const PASSWORD_MAX = 64;
