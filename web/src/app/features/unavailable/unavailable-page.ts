import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { safeReturnUrl } from '../../core/navigation/return-url';

/** Shown when the session could not be checked because the service failed (not a sign-out). */
@Component({
  selector: 'acts-unavailable-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule],
  template: `
    <div class="unavailable">
      <h1>ACTS is unavailable</h1>
      <p role="status">
        {{ retrying() ? 'Checking the service…' : 'The service could not be reached. Your session was not signed out.' }}
      </p>
      <button mat-flat-button type="button" [disabled]="retrying()" (click)="retry()">Retry</button>
    </div>
  `,
  styles: `
    .unavailable {
      max-width: 480px;
      margin: 64px auto;
      padding: 16px;
    }
  `,
})
export class UnavailablePage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly retrying = signal(false);

  protected async retry(): Promise<void> {
    this.retrying.set(true);
    await this.auth.bootstrap();
    this.retrying.set(false);
    if (this.auth.status() !== 'unavailable') {
      await this.router.navigateByUrl(safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')));
    }
  }
}
