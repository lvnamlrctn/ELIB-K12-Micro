import { Component, signal, inject, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookApiService } from '../services/book-api.service';
import { MenuItem } from '../services/models';
import { AuthService } from '../services/auth.service';
import { TenantService } from '../services/tenant.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'opac-header',
  imports: [RouterLink, RouterLinkActive, TranslateModule, CommonModule],
  templateUrl: './header.html'
})
export class HeaderComponent implements OnInit {
  private bookApi = inject(BookApiService);
  public authService = inject(AuthService);
  public tenant = inject(TenantService);
  public translate = inject(TranslateService);
  
  isMobileMenuOpen = signal(false);
  activeDropdown = signal<string | null>(null);
  menuItems = signal<MenuItem[]>([]);

  ngOnInit() {
    this.bookApi.getMenuItems().subscribe(items => {
      this.menuItems.set(items);
    });
  }

  toggleMobileMenu() {
    this.isMobileMenuOpen.update(v => !v);
  }

  toggleDropdown(menu: string) {
    if (this.activeDropdown() === menu) {
      this.activeDropdown.set(null);
    } else {
      this.activeDropdown.set(menu);
    }
  }

  closeMenu() {
    this.isMobileMenuOpen.set(false);
    this.activeDropdown.set(null);
  }

  logout() {
    this.authService.logout();
    this.closeMenu();
  }
}
