import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

const TOKEN_KEY = 'auth_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private tokenSignal = signal<string | null>(this.getStoredToken());

  isLoggedIn = computed(() => {
    const token = this.tokenSignal();
    if (!token) return false;
    return !this.isTokenExpired(token);
  });

  login(password: string): Observable<{ token: string }> {
    return this.http.post<{ token: string }>('/api/auth/login', { password }).pipe(
      tap(response => {
        localStorage.setItem(TOKEN_KEY, response.token);
        this.tokenSignal.set(response.token);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.tokenSignal.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  private getStoredToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private isTokenExpired(token: string): boolean {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 < Date.now();
    } catch {
      return true;
    }
  }
}
