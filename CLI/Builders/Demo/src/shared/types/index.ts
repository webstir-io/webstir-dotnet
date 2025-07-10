// Base API response type - matches webstir's base type
export interface ApiResponse<T> {
  data?: T;
  error?: string;
}