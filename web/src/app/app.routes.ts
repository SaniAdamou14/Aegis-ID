import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/overview/overview.component').then((m) => m.OverviewComponent),
    title: 'Aegis-ID - Overview',
  },
  {
    path: 'findings',
    loadComponent: () => import('./pages/findings/findings.component').then((m) => m.FindingsComponent),
    title: 'Aegis-ID - Findings',
  },
  { path: '**', redirectTo: '' },
];
