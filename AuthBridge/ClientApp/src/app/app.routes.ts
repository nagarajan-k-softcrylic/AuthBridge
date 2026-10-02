import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { HomeComponent } from './auth/home/home.component';
import { authGuard, applicationAdminGuard, guestGuard } from './auth/auth.guard';
import { ApplicationSearchComponent } from './applications/application-search/application-search.component';
import { MyApplicationsComponent } from './applications/my-applications/my-applications.component';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
  { path: 'register', component: RegisterComponent, canActivate: [guestGuard] },
  { path: 'home', component: HomeComponent, canActivate: [authGuard] },
  { path: 'my-applications', component: MyApplicationsComponent, canActivate: [authGuard] },
  { path: 'applications', component: ApplicationSearchComponent, canActivate: [applicationAdminGuard] },
  { path: '**', redirectTo: 'login' }
];
