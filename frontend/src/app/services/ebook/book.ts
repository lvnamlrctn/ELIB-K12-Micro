import { Injectable } from '@angular/core';
import { delay, of } from 'rxjs';
import { DataTableParams, DataTableResponse } from '../../models/shared/datatable';

export interface Book {
  id: string;
  image: string;
  title: string;
  author: string;
  year: number;
  publisher: string;
}

@Injectable({
  providedIn: 'root'
})
export class BookStore {
  private allBooks: Book[] = [
    { id: '1', image: 'https://covers.openlibrary.org/b/id/8225266-M.jpg', title: 'Clean Code', author: 'Robert C. Martin', year: 2008, publisher: 'Prentice Hall' },
    { id: '2', image: 'https://covers.openlibrary.org/b/id/8228691-M.jpg', title: 'The Pragmatic Programmer', author: 'Andrew Hunt', year: 1999, publisher: 'Addison-Wesley' },
    { id: '3', image: 'https://covers.openlibrary.org/b/id/10580252-M.jpg', title: 'Design Patterns', author: 'Erich Gamma', year: 1994, publisher: 'Addison-Wesley' },
    { id: '4', image: 'https://covers.openlibrary.org/b/id/8259441-M.jpg', title: 'Refactoring', author: 'Martin Fowler', year: 1999, publisher: 'Addison-Wesley' },
    { id: '5', image: 'https://covers.openlibrary.org/b/id/12832049-M.jpg', title: 'Head First Design Patterns', author: 'Eric Freeman', year: 2004, publisher: "O'Reilly Media" },
    { id: '6', image: 'https://covers.openlibrary.org/b/id/8225266-M.jpg', title: 'Effective Java', author: 'Joshua Bloch', year: 2001, publisher: 'Addison-Wesley' },
    { id: '7', image: 'https://covers.openlibrary.org/b/id/8228691-M.jpg', title: 'Introduction to Algorithms', author: 'Thomas H. Cormen', year: 1990, publisher: 'MIT Press' },
    { id: '8', image: 'https://covers.openlibrary.org/b/id/10580252-M.jpg', title: 'Code Complete', author: 'Steve McConnell', year: 1993, publisher: 'Microsoft Press' },
    { id: '9', image: 'https://covers.openlibrary.org/b/id/8259441-M.jpg', title: 'Mythical Man-Month', author: 'Fred Brooks', year: 1975, publisher: 'Addison-Wesley' },
    { id: '10', image: 'https://covers.openlibrary.org/b/id/12832049-M.jpg', title: 'Domain-Driven Design', author: 'Eric Evans', year: 2003, publisher: 'Addison-Wesley' },
    { id: '11', image: 'https://covers.openlibrary.org/b/id/8225266-M.jpg', title: 'Test Driven Development', author: 'Kent Beck', year: 2002, publisher: 'Addison-Wesley' },
    { id: '12', image: 'https://covers.openlibrary.org/b/id/8228691-M.jpg', title: 'Structure and Interpretation of Computer Programs', author: 'Harold Abelson', year: 1984, publisher: 'MIT Press' },
    { id: '13', image: 'https://covers.openlibrary.org/b/id/10580252-M.jpg', title: 'Compilers: Principles, Techniques, and Tools', author: 'Alfred Aho', year: 1986, publisher: 'Addison-Wesley' },
    { id: '14', image: 'https://covers.openlibrary.org/b/id/8259441-M.jpg', title: 'Working Effectively with Legacy Code', author: 'Michael Feathers', year: 2004, publisher: 'Prentice Hall' },
    { id: '15', image: 'https://covers.openlibrary.org/b/id/12832049-M.jpg', title: 'Cracking the Coding Interview', author: 'Gayle Laakmann McDowell', year: 2008, publisher: 'CareerCup' },
    { id: '16', image: 'https://covers.openlibrary.org/b/id/8225266-M.jpg', title: 'Designing Data-Intensive Applications', author: 'Martin Kleppmann', year: 2017, publisher: "O'Reilly Media" },
    { id: '17', image: 'https://covers.openlibrary.org/b/id/8259441-M.jpg', title: 'The Art of Computer Programming', author: 'Donald Knuth', year: 1968, publisher: 'Addison-Wesley' },
    { id: '18', image: 'https://covers.openlibrary.org/b/id/12832049-M.jpg', title: 'Patterns of Enterprise Application Architecture', author: 'Martin Fowler', year: 2002, publisher: 'Addison-Wesley' },
  ];

  getBookById(id: string) {
    const book = this.allBooks.find(b => b.id === id);
    return of(book).pipe(delay(200));
  }

  addBook(book: Omit<Book, 'id'>) {
    const newBook = { ...book, id: Math.random().toString(36).substr(2, 9) };
    this.allBooks.unshift(newBook);
    return of(newBook).pipe(delay(300));
  }

  updateBook(id: string, updates: Partial<Book>) {
    const index = this.allBooks.findIndex(b => b.id === id);
    if (index !== -1) {
      this.allBooks[index] = { ...this.allBooks[index], ...updates };
      return of(this.allBooks[index]).pipe(delay(300));
    }
    throw new Error('Book not found');
  }

  deleteBook(id: string) {
    const index = this.allBooks.findIndex(b => b.id === id);
    if (index !== -1) {
      this.allBooks.splice(index, 1);
      return of(true).pipe(delay(300));
    }
    return of(false);
  }

  getBooks(params: DataTableParams) {
    let filtered = [...this.allBooks];

    // Simple search
    if (params.search.value) {
      const searchValue = params.search.value.toLowerCase();
      filtered = filtered.filter(b => 
        b.title.toLowerCase().includes(searchValue) || 
        b.author.toLowerCase().includes(searchValue) ||
        b.publisher.toLowerCase().includes(searchValue)
      );
    }

    // Simple sort (based on common columns, adjust as needed)
    if (params.order && params.order.length > 0) {
      const order = params.order[0];
      const columnMap: Record<number, keyof Book> = {
        2: 'title',
        3: 'author',
        4: 'year',
        5: 'publisher'
      };
      
      const col = columnMap[order.column];
      if (col) {
        filtered.sort((a, b) => {
          const valA = a[col];
          const valB = b[col];
          if (valA < valB) return order.dir === 'asc' ? -1 : 1;
          if (valA > valB) return order.dir === 'asc' ? 1 : -1;
          return 0;
        });
      }
    }

    const start = params.start;
    const length = params.length;
    const pagedData = filtered.slice(start, start + length);

    const response: DataTableResponse<Book> = {
      draw: params.draw,
      recordsTotal: this.allBooks.length,
      recordsFiltered: filtered.length,
      data: pagedData
    };

    return of(response).pipe(delay(500));
  }
}
