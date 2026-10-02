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
  // Angular preserves query params across a static redirectTo by default, so a server-side
  // redirect to a non-SPA path like "/Account/Login?ReturnUrl=..." (IdentityServer's default
  // interactive login redirect) still lands on the login form with the OIDC callback URL intact
  // for navigateAfterLogin() to use.
  { path: '**', redirectTo: 'login' }
];
