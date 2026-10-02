import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApplicationDto, ApplicationService } from '../application.service';

@Component({
  selector: 'app-application-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './application-search.component.html',
  styleUrl: './application-search.component.scss'
})
export class ApplicationSearchComponent implements OnInit {
  applications: ApplicationDto[] = [];
  searchTerm = '';
  page = 1;
  pageSize = 10;
  totalCount = 0;

  /** The application currently showing its inline "Assign User" email form, or null if none. */
  assigningApplicationId: string | null = null;
  assignEmail = '';
  assignMessage: string | null = null;
  assignError: string | null = null;

  constructor(private applicationService: ApplicationService, private router: Router) {}

  ngOnInit(): void {
    this.loadApplications();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  loadApplications(): void {
    this.applicationService.search(this.searchTerm, this.page, this.pageSize).subscribe({
      next: (result) => {
        this.applications = result.items;
        this.totalCount = result.totalCount;
      },
      error: (err) => {
        if (err.status === 403) {
          this.router.navigateByUrl('/home');
        }
      }
    });
  }

  onSearch(): void {
    this.page = 1;
    this.loadApplications();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) {
      return;
    }

    this.page = page;
    this.loadApplications();
  }

  startAssign(applicationId: string): void {
    this.assigningApplicationId = applicationId;
    this.assignEmail = '';
    this.assignMessage = null;
    this.assignError = null;
  }

  cancelAssign(): void {
    this.assigningApplicationId = null;
  }

  confirmAssign(applicationId: string): void {
    if (!this.assignEmail) {
      return;
    }

    this.assignError = null;
    this.applicationService.assignByEmail({ email: this.assignEmail, applicationId }).subscribe({
      next: () => {
        this.assignMessage = `Access granted to ${this.assignEmail}.`;
        this.assigningApplicationId = null;
        this.loadApplications();
      },
      error: (err) => {
        this.assignError = err?.error?.message ?? 'Unable to assign application.';
      }
    });
  }
}
