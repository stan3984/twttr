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

export interface TPost {
  id: string;
  authorId: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  inReplyToId: string | null;
}

export interface TCreatePostRequest {
  content: string;
  inReplyToId?: string;
}

export interface TUser {
  id: string;
  username: string;
  displayName: string;
}

// Mirrored from AuthController.
export const USERNAME_MIN = 8;
export const USERNAME_MAX = 24;
export const PASSWORD_MIN = 12;
export const PASSWORD_MAX = 64;

// Mirrored from PostsController.
export const CONTENT_MIN = 2;
export const CONTENT_MAX = 280;
export const POSTS_PAGE_SIZE = 20;
