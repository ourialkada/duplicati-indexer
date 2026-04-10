import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ShipButton, ShipCard, ShipFormField, ShipAlert } from '@ship-ui/core';
import LogoComponent from '../logo/logo.component';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, ShipButton, ShipCard, ShipFormField, ShipAlert, LogoComponent],
  template: `
    <div class="login-container">
      <sh-card class="login-card">
        <div class="login-header">
          <app-logo />
        </div>

        @if (error()) {
          <sh-alert color="error" variant="outlined" class="login-error">
            {{ error() }}
          </sh-alert>
        }

        <form (ngSubmit)="onSubmit()" class="login-form">
          <sh-form-field label="Password">
            <input
              type="password"
              [ngModel]="password()"
              (ngModelChange)="password.set($event)"
              name="password"
              placeholder="Enter admin password"
              (keydown.enter)="onSubmit()"
              autofocus
            />
          </sh-form-field>

          <button
            shButton
            class="primary raised login-btn"
            type="submit"
            [disabled]="isLoading() || !password().trim()"
          >
            {{ isLoading() ? 'Signing in...' : 'Sign in' }}
          </button>
        </form>
      </sh-card>
    </div>
  `,
  styles: `
    @use '@ship-ui/core/styles/helpers' as *;

    :host {
      display: flex;
      height: 100vh;
      width: 100%;
    }

    .login-container {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 100%;
      background: var(--base-0);
    }

    .login-card {
      width: 100%;
      max-width: 400px;
      padding: p2r(32);
    }

    .login-header {
      text-align: center;
      margin-bottom: p2r(24);
    }

    .login-error {
      margin-bottom: p2r(16);
    }

    .login-form {
      display: flex;
      flex-direction: column;
      gap: p2r(16);
    }

    sh-form-field {
      width: 100%;
    }

    .login-btn {
      width: 100%;
    }
  `,
})
export default class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  password = signal('');
  error = signal('');
  isLoading = signal(false);

  onSubmit(): void {
    const pw = this.password().trim();
    if (!pw || this.isLoading()) return;

    this.isLoading.set(true);
    this.error.set('');

    this.authService.login(pw).subscribe({
      next: () => {
        this.router.navigate(['/chat']);
      },
      error: () => {
        this.error.set('Invalid password');
        this.isLoading.set(false);
      },
    });
  }
}
