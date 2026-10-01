import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService, AuthUser } from '../auth.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  user: AuthUser | null = null;

  constructor(private authService: AuthService, private router: Router) {
    // The auth guard already resolved /api/auth/me before this component loads, so the
    // user is cached in memory; fetchCurrentUser() returns it without another round trip.
    this.authService.fetchCurrentUser().subscribe((user) => (this.user = user));
  }

  logout(): void {
    this.authService.logout().subscribe(() => this.router.navigateByUrl('/login'));
  }
}
