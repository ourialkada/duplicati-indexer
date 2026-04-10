import { Routes } from '@angular/router';
import { authGuard } from './services/auth.guard';

const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./components/login/login.component'),
  },
  {
    path: 'chat',
    loadComponent: () => import('./components/chat/chat.component'),
    canActivate: [authGuard],
  },
  {
    path: 'admin',
    loadComponent: () => import('./components/admin/admin.component'),
    canActivate: [authGuard],
  },
  { path: '', redirectTo: 'chat', pathMatch: 'full' },
  { path: '**', redirectTo: 'chat' },
];

export default routes;
