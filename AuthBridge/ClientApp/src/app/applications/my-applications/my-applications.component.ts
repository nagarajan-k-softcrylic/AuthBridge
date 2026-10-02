import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApplicationService, UserApplicationDto } from '../application.service';
import { AuthService } from '../../auth/auth.service';

/**
 * Fixed honeycomb shape (top row, middle row, bottom row) matching the reference layout:
 * https://codepen.io/joshhowenstine/pen/kWEKXg. Always rendered in full; slots beyond the
 * user's assigned app count are shown as empty placeholder hexagons.
 */
const ROW_PATTERN = [3, 4, 3];

/** A palette cycled through per tile so each hexagon gets a distinct flat color. */
const TILE_COLORS = ['#6a5bff', '#39c4ff', '#0854a0', '#107e3e', '#e9730c', '#bb0000'];

/**
 * Post-login landing page: shows the current user's assigned applications as a honeycomb of
 * hexagon launch tiles (styling modeled on https://codepen.io/joshhowenstine/pen/kWEKXg).
 * Only applications returned by `my-applications` (i.e. ones the user has been granted access
 * to) appear here, so every tile shown gets a "Launch" action.
 */
@Component({
  selector: 'app-my-applications',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './my-applications.component.html',
  styleUrl: './my-applications.component.scss'
})
export class MyApplicationsComponent implements OnInit {
  apps: UserApplicationDto[] = [];
  loading = true;
  errorMessage: string | null = null;
  launchingApplicationId: string | null = null;

  constructor(private applicationService: ApplicationService, private authService: AuthService) {}

  get isApplicationAdmin(): boolean {
    return this.authService.isApplicationAdmin();
  }

  /**
   * Lays the user's apps out into the fixed honeycomb shape (3 / 4 / 3). Unfilled slots are
   * `null` and rendered as empty placeholder hexagons so the honeycomb always looks complete.
   */
  get rows(): (UserApplicationDto | null)[][] {
    const rows: (UserApplicationDto | null)[][] = [];
    let cursor = 0;
    for (const rowSize of ROW_PATTERN) {
      const row: (UserApplicationDto | null)[] = [];
      for (let i = 0; i < rowSize; i++) {
        row.push(cursor < this.apps.length ? this.apps[cursor] : null);
        cursor++;
      }
      rows.push(row);
    }
    return rows;
  }

  tileColor(app: UserApplicationDto): string {
    const index = this.apps.indexOf(app);
    return TILE_COLORS[index % TILE_COLORS.length];
  }

  ngOnInit(): void {
    this.applicationService.myApplications().subscribe({
      next: (apps) => {
        this.apps = apps;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Unable to load your applications.';
        this.loading = false;
      }
    });
  }

  launch(app: UserApplicationDto): void {
    this.errorMessage = null;
    this.launchingApplicationId = app.applicationId;

    this.applicationService.launch(app.applicationId).subscribe({
      next: (res) => {
        this.launchingApplicationId = null;
        window.open(res.launchUrl, '_blank', 'noopener');
      },
      error: () => {
        this.launchingApplicationId = null;
        this.errorMessage = `Unable to launch ${app.applicationName}.`;
      }
    });
  }
}
