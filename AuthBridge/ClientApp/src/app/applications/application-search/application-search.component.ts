import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
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

  /** Loads the whole catalog in one page since every tile needs to be visible/selectable at once. */
  page = 1;
  pageSize = 100;
  totalCount = 0;

  /** Ids of applications currently selected via tile click (checkbox-style multi-select). */
  selectedApplicationIds = new Set<string>();

  assignEmail = '';
  assignMessage: string | null = null;
  assignError: string | null = null;
  assigning = false;

  constructor(private applicationService: ApplicationService, private router: Router) {}

  ngOnInit(): void {
    this.loadApplications();
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
    this.loadApplications();
  }

  isSelected(applicationId: string): boolean {
    return this.selectedApplicationIds.has(applicationId);
  }

  /** Clicking a tile toggles its selection checkbox. */
  toggleSelection(applicationId: string): void {
    if (this.selectedApplicationIds.has(applicationId)) {
      this.selectedApplicationIds.delete(applicationId);
    } else {
      this.selectedApplicationIds.add(applicationId);
    }

    this.assignMessage = null;
    this.assignError = null;
  }

  clearSelection(): void {
    this.selectedApplicationIds.clear();
    this.assignEmail = '';
    this.assignMessage = null;
    this.assignError = null;
  }

  /** Assigns every selected application to the entered user's email. */
  confirmAssign(): void {
    if (!this.assignEmail || this.selectedApplicationIds.size === 0) {
      return;
    }

    this.assignError = null;
    this.assigning = true;

    const requests = Array.from(this.selectedApplicationIds).map((applicationId) =>
      this.applicationService.assignByEmail({ email: this.assignEmail, applicationId })
    );

    forkJoin(requests).subscribe({
      next: () => {
        this.assignMessage = `Access granted to ${this.assignEmail} for ${requests.length} application(s).`;
        this.assigning = false;
        this.selectedApplicationIds.clear();
        this.assignEmail = '';
        this.loadApplications();
      },
      error: (err) => {
        this.assignError = err?.error?.message ?? 'Unable to assign one or more applications.';
        this.assigning = false;
      }
    });
  }
}
