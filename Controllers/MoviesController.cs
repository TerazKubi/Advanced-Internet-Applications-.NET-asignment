using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIA_tutorial_2.Controllers;
[ApiController]
[Route("[controller]")]
public class MoviesController : ControllerBase
{
    private MoviesContext dbContext;

    public MoviesController(){
        dbContext = new MoviesContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    [HttpGet("GetMovieById/{MovieID}")]
    public Movie GetMovieById(int MovieID){
        return dbContext.Movies.Select(m => new Movie{MovieID = m.MovieID, Title = m.Title, 
                                            Genres = m.Genres.Select(g => new Genre{GenreID = g.GenreID, Name = g.Name}).ToList()})
                                            .Where(m => m.MovieID == MovieID).ToList().First();

    }


    [HttpGet("GetMovies/{Count}")]
    public List<Movie> GetMovies(int Count){
        int take = Count;
        return dbContext.Movies.Select(movie => new Movie{MovieID = movie.MovieID, Title = movie.Title, 
                                            Genres = movie.Genres.Select(genre => new Genre{GenreID = genre.GenreID, Name = genre.Name})
                                            .ToList()})
                                            .Take(take)
                                            .ToList();
    }

    [HttpGet("GetAllMovies")]
    public List<Movie> GetAllMovies(){
        return dbContext.Movies.Select(m => new Movie{MovieID = m.MovieID, Title = m.Title, 
                                            Genres = m.Genres.Select(g => new Genre{GenreID = g.GenreID, Name = g.Name}).ToList()})
                                            .ToList();
    }

    double cosineSimilarity(double[] vector1, double[] vector2){
        int N = vector1.Count();

        double licznik = 0;
        for (int i=0; i < N; i++) licznik += vector1[i] * vector2[i]; 

        double sum1=0;
        double sum2=0;

        for (int i=0; i < N; i++) sum1 += Math.Pow(vector1[i], 2);
        for (int i=0; i < N; i++) sum2 += Math.Pow(vector2[i], 2);

        double mianownik = Math.Sqrt(sum1) * Math.Sqrt(sum2);

        return licznik / mianownik;
    }
    private Dictionary<string, object> tokenizeRatings(string line){

        var tokens = line.Replace("\r", "").Split(",");
        Dictionary<string, object> res = new Dictionary<string, object>();
        if (tokens.Length != 4) return res;

        res.Add("userID", int.Parse(tokens[0]));
        res.Add("movieID", int.Parse(tokens[1]));
        res.Add("rating", float.Parse(tokens[2].Replace(".", ",")));
        return res;
    }



    [HttpPost("UploadMovieCsv")]
    public string PostMovies(IFormFile inputFile)
    {
        var strm = inputFile.OpenReadStream();
        byte[] buffer = new byte[inputFile.Length];
        strm.Read(buffer, 0, (int)inputFile.Length);
        string fileContent = System.Text.Encoding.Default.GetString(buffer);
        strm.Close();

        List<Movie> globalMovies = new List<Movie>();
        List<Genre> globalGenres = new List<Genre>();

        //load data from db
        foreach (Movie m in dbContext.Movies){ globalMovies.Add(m);}
        foreach (Genre g in dbContext.Genres){ globalGenres.Add(g);}

        int globalMoviesInitCount = globalMovies.Count();
        int globalGenresInitCount = globalGenres.Count();
        
        bool skip_header = true;
        foreach (string line in fileContent.Split('\n'))
        {
            if (skip_header)
            {
                skip_header = false;
                continue;
            }
            
            var tokens = line.Replace("\r", "").Split(",");
            if (tokens.Length != 3) continue;
            string MovieID = tokens[0];
            string MovieName = tokens[1];
            string[] Genres = tokens[2].Split("|");

            List<Genre> movieGenres = new List<Genre>();

            foreach (string genre in Genres)
            {
                Genre g = new Genre();
                g.Name = genre;

                if (!globalGenres.Any(e => e.Name == g.Name)) globalGenres.Add(g);
                
                
                IEnumerable<Genre> results = globalGenres.Where(e => e.Name == g.Name);
                if (results.Count() > 0) movieGenres.Add(results.First());
            }

            Movie m = new Movie();
            m.MovieID = int.Parse(MovieID);
            m.Title = MovieName;
            m.Genres = movieGenres;

            if (!globalMovies.Any(gm => gm.MovieID == m.MovieID)) globalMovies.Add(m);
        }

        for(int i = globalGenresInitCount; i < globalGenres.Count(); i++) dbContext.Genres.Add(globalGenres[i]);      
        dbContext.SaveChanges();

        for(int i = globalMoviesInitCount; i < globalMovies.Count(); i++) dbContext.Movies.Add(globalMovies[i]);       
        dbContext.SaveChanges();

        return "OK";
    }

    [HttpPost("UploadRatingsCsv")]
    public string PostRatings(IFormFile inputFile)
    {
        var strm = inputFile.OpenReadStream();
        byte[] buffer = new byte[inputFile.Length];
        strm.Read(buffer, 0, (int)inputFile.Length);
        string fileContent = System.Text.Encoding.Default.GetString(buffer);
        strm.Close();

        List<Rating> globalRatings = new List<Rating>();
        List<User> globalUsers = new List<User>();
        List<Movie> globalMovies = new List<Movie>();

        foreach (User u in dbContext.Users){ globalUsers.Add(u);}
        foreach (Rating r in dbContext.Ratings){ globalRatings.Add(r);}
        foreach (Movie m in dbContext.Movies){ globalMovies.Add(m);}


        int globalUsersInitCount = globalUsers.Count();
        int globalRatingsInitCount = globalRatings.Count();

        bool skip_header = true;
        foreach (string line in fileContent.Split('\n'))
        {
            if (skip_header)
            {
                skip_header = false;
                continue;
            }
            
            Dictionary<string, object> rating = tokenizeRatings(line);
            if(rating.Count() == 0) continue;

            User u = new User();
            u.UserID = (int) rating["userID"];
            u.Name = "user" + (int) rating["userID"];

            if(!globalUsers.Any(user => user.UserID == u.UserID)) globalUsers.Add(u);

        }

        for(int i = globalUsersInitCount; i< globalUsers.Count(); i++) dbContext.Users.Add(globalUsers[i]);        
        dbContext.SaveChanges();

        skip_header = true;
        foreach(string line in fileContent.Split('\n')){

            if (skip_header)
            {
                skip_header = false;
                continue;
            }

            Dictionary<string, object> rating = tokenizeRatings(line);
            if(rating.Count() == 0) continue;

            IEnumerable<User> results_u = dbContext.Users.Where(user => user.UserID == (int) rating["userID"]);
            if(results_u.Count() == 0) throw new Exception("Brak takiego użytkownika"); 
            User ratingUser = results_u.First();

            IEnumerable<Movie> results = globalMovies.Where(movie => movie.MovieID == (int) rating["movieID"]);
            if(results.Count() == 0) continue; 
            Movie ratedMovie = results.First();

            Rating r = new Rating();
            r.RatingUser = ratingUser;
            r.RatedMovie = ratedMovie;
            r.RatingValue = (float) rating["rating"];

            globalRatings.Add(r);
        }


        for(int i = globalRatingsInitCount; i < globalRatings.Count(); i++) dbContext.Ratings.Add(globalRatings[i]);
        dbContext.SaveChanges();

        return "OK";
    }

    [HttpGet("GetAllGenres")]
    public List<Genre> GetAllGenres()
    {
        return dbContext.Genres.ToList();
    }



    [HttpGet("GetMoviesByName/{search_phrase}")]
    public IEnumerable<Movie> GetMoviesByName(string search_phrase)
    {
        return dbContext.Movies.Where(movie => movie.Title.Contains(search_phrase));
    }



    [HttpGet("GetMoviesByGenre/{search_phrase}")]
    public List<Movie> GetMoviesByGenre(string search_phrase)
    {
        return dbContext.Movies.Where(
        movie => movie.Genres.Any(genre => genre.Name.Contains(search_phrase))
        ).ToList();
    }


    List<Movie> GetMoviesByGenreID(int GenreID)
    {
        return dbContext.Movies.Where(
        movie => movie.Genres.Any(genre => genre.GenreID == GenreID)
        ).ToList();
    }
    //========================================================================================================

    //T1.1
    [HttpGet("GetMovieGenres/{MovieID}")]
    public List<Genre> GetMovieGenres(int MovieID)
    {
        return GetMovieById(MovieID).Genres.ToList();
    }


    //T1.2
    [HttpGet("GetMovieGenresVector/{MovieID}")]
    public double[] GetMovieGenresVector(int MovieID)
    {
        var allGenres = GetAllGenres();
        double[] vector = new double[allGenres.Count()];
        var movieGenres = GetMovieById(MovieID).Genres.ToList();
        int i = 0;
        foreach(Genre g in allGenres)
        {
            if(movieGenres.Any(m => m.GenreID == g.GenreID)) vector[i] = 1;
            else vector[i] = 0;

            i++;
        }

        return vector;
    }


    //T1.3
    [HttpGet("CompareMovies/{id1}/{id2}")]
    public double CompareMovies(int id1, int id2)
    {
        double[] vector1 = GetMovieGenresVector(id1);
        double[] vector2 = GetMovieGenresVector(id2);

        return cosineSimilarity(vector1, vector2);
    }


    //T1.4
    [HttpGet("GetMoviesWithSharedGenres/{MovieID}/{Count}")]
    public List<Movie> GetMoviesWithSharedGenres(int MovieID, int Count=1000 )
    {
        Movie targetMovie = GetMovieById(MovieID);
        // List<Genre> targetGenres = GetGenresForMovie(id);

        List<Movie> allMovies = GetAllMovies();

        List<Movie> res = new List<Movie>();

        foreach(Movie m in allMovies){

            if (m.MovieID == targetMovie.MovieID) continue;

            // List<Genre> genres = GetGenresForMovie(m.MovieID);
            foreach(Genre g in m.Genres){

                if(targetMovie.Genres.Any(gen => gen.GenreID == g.GenreID)){
                    res.Add(m);     
                    break;
                } 

            }
            if(res.Count() >= Count) break;
        }
        return res;  
    }



    //T1.5
    [HttpGet("GetSimilarMovies/{MovieID}/{Threshold}/{Count}")]
    public List<Movie> GetSimilarMovies(int MovieID, double Threshold, int Count)
    {
        List<Movie> movieList = GetMoviesWithSharedGenres(MovieID);
        double[] vector1 = GetMovieGenresVector(MovieID);

        List<Movie> res = new List<Movie>();

        foreach(Movie movie in movieList)
        {
            double[] vector2 = GetMovieGenresVector(movie.MovieID);
            double similarity = cosineSimilarity(vector1, vector2);
            // Console.WriteLine("movieID: " +MovieID+ ", similarity: " + similarity + " to: " + movie.MovieID +": " + movie.Title);
            if (similarity >= Threshold)
            {
                res.Add(movie);
                if (res.Count() >= Count) break;
            } 
        }

        return res;  
    }



    //T1.6
    [HttpGet("GetMoviesRatedByUser/{UserID}")]
    public List<Movie> GetMoviesRatedByUser(int UserID)
    {
        List<Movie> res = new List<Movie>();
        
        var userRatings = dbContext.Ratings.Select(rating => new Rating{RatingID = rating.RatingID, RatingValue = rating.RatingValue,
                        RatedMovie = rating.RatedMovie,
                        RatingUser = rating.RatingUser})
                        .Where(r => r.RatingUser.UserID == UserID);
                                             
        
        foreach(Rating rating in userRatings) if(rating.RatedMovie != null) res.Add(rating.RatedMovie);
        
        return res;  
    }


    //T1.7
    [HttpGet("GetMoviesRatedByUserSorted/{UserID}")]
    public List<Movie> GetMoviesRatedByUserSorted(int UserID)
    {
        List<Movie> res = new List<Movie>();
        
        var userRatings = dbContext.Ratings.Where(r => r.RatingUser.UserID == UserID)
                                             .Include(r => r.RatedMovie)
                                             .OrderByDescending(r => r.RatingValue).ToList();
        
        // foreach(Rating r in userRatings) Console.WriteLine(r.RatedMovie.Title + ": " + r.RatingValue);

        foreach(Rating r in userRatings) if(r.RatedMovie != null) res.Add(r.RatedMovie);
        
        return res;  
    }


    //T1.8
    [HttpGet("GetMoviesSimilarToHighestRated/{userID}/{threshold}/{count}")]
    public List<Movie> GetMoviesSimilarToHighestRatedByUser(int userID, double threshold, int count)
    {
        var highestRatedMovie = GetMoviesRatedByUserSorted(userID).First();

        Console.WriteLine("\n\nhighest rated movie: " + highestRatedMovie.MovieID+": "+highestRatedMovie.Title);
        
        return GetSimilarMovies(highestRatedMovie.MovieID, threshold, count);
    }


    //T1.9
    [HttpGet("GetMoviesSimilarToHighestRatedSized/{userID}/{threshold}/{count}/{size}")]
    public List<Movie> GetMoviesSimilarToHighestRatedByUserSized(int userID, double threshold, int count, int size)
    {
        return GetMoviesSimilarToHighestRatedByUser(userID, threshold, count).Take(size).ToList();
    }



    //T2 bonus ===============================================================================================================================

    double[] GetAvgRatingGenreVector(int userID){
        double[] vector = new double[dbContext.Genres.Count()];

        double[] sum = new double[dbContext.Genres.Count()];
        int[] count = new int[dbContext.Genres.Count()];

        List<Rating> userRatings = dbContext.Ratings.Include(r => r.RatedMovie).Include(r=> r.RatedMovie.Genres).Include(r => r.RatingUser)
                                                    .Where(r => r.RatingUser.UserID == userID).ToList();

        foreach(Rating r in userRatings){
          
            foreach(Genre g in r.RatedMovie.Genres){
                sum[g.GenreID-1] += r.RatingValue;
                count[g.GenreID-1] += 1;
            }

        }

        for(int i=0; i<vector.Count(); i++) if(count[i] != 0) vector[i] = sum[i] / count[i];

        return vector;
    }



    [HttpGet("GetRecomendationBasedOnSimilarUsers/{userID}/{threshold}/{size}")]
    public List<Movie> asd(int userID, double threshold, int size)
    {
        double[] vec = GetAvgRatingGenreVector(userID);
        
        List<Movie> res = new List<Movie>();

        List<Movie> moviesRatedByUser = GetMoviesRatedByUser(userID);
        

        foreach(User u in dbContext.Users.ToList()){
            if(u.UserID == userID) continue;

            double[] vector = GetAvgRatingGenreVector(u.UserID);

            double similarity = cosineSimilarity(vec, vector);
            if(similarity < threshold) continue;

            // Console.WriteLine(u.UserID+": similarity: " + similarity); 
            
            Movie highestRatedMovie = GetMoviesRatedByUserSorted(u.UserID).First();
            
            Movie m = new Movie();
            m.MovieID = highestRatedMovie.MovieID;
            m.Title = highestRatedMovie.Title;
            
            if(moviesRatedByUser.Contains(m)) continue;
            
            if(res.Contains(m)) continue;
                
            res.Add(m);    
            

            if(res.Count() >= size) break;
        }


        return res;
    }

}
