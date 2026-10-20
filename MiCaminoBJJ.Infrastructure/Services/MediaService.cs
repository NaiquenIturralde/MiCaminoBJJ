using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MediaService> _logger;

    public MediaService(AppDbContext context, ILogger<MediaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MediaAsset> CreateAsync(MediaAsset mediaAsset)
    {
        _context.MediaAssets.Add(mediaAsset);
        await _context.SaveChangesAsync();
        return mediaAsset;
    }

    public async Task<MediaAsset?> GetByIdAsync(int mediaAssetId)
    {
        return await _context.MediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAssetId && !m.IsDeleted);
    }

    public async Task SoftDeleteAsync(int mediaAssetId)
    {
        var media = await _context.MediaAssets.FindAsync(mediaAssetId);
        if (media != null)
        {
            media.IsDeleted = true;
            media.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<MediaAsset>> GetOfficialByTechniqueAsync(int techniqueId)
    {
        return await _context.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.TechniqueId == techniqueId && !tm.MediaAsset.IsDeleted)
            .OrderBy(tm => tm.DisplayOrder)
            .Select(tm => tm.MediaAsset)
            .ToListAsync();
    }

    public async Task<List<TechniqueMedia>> GetOfficialMediaEntriesAsync(int techniqueId)
    {
        return await _context.TechniqueMedia
            .Include(tm => tm.MediaAsset)
            .Where(tm => tm.TechniqueId == techniqueId && !tm.MediaAsset.IsDeleted)
            .OrderBy(tm => tm.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<MediaAsset>> GetPersonalByTechniqueAsync(int userId, int techniqueId)
    {
        return await _context.UserTechniqueResources
            .Include(r => r.MediaAsset)
            .Where(r => r.UserId == userId && r.TechniqueId == techniqueId && !r.MediaAsset.IsDeleted)
            .OrderBy(r => r.DisplayOrder)
            .Select(r => r.MediaAsset)
            .ToListAsync();
    }

    public async Task<TechniqueMedia> AddOfficialMediaAsync(int techniqueId, int mediaAssetId, bool isCover, int displayOrder)
    {
        var entry = new TechniqueMedia
        {
            TechniqueId = techniqueId,
            MediaAssetId = mediaAssetId,
            IsCover = isCover,
            DisplayOrder = displayOrder
        };

        _context.TechniqueMedia.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task RemoveOfficialMediaAsync(int techniqueId, int mediaAssetId)
    {
        var entry = await _context.TechniqueMedia
            .FirstOrDefaultAsync(tm => tm.TechniqueId == techniqueId && tm.MediaAssetId == mediaAssetId);

        if (entry != null)
        {
            _context.TechniqueMedia.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetCoverAsync(int techniqueId, int mediaAssetId)
    {
        // Unset any current cover for this technique
        var currentCover = await _context.TechniqueMedia
            .Where(tm => tm.TechniqueId == techniqueId && tm.IsCover)
            .ToListAsync();

        foreach (var item in currentCover)
        {
            item.IsCover = false;
        }

        // Set the new cover if the entry exists
        var entry = await _context.TechniqueMedia
            .FirstOrDefaultAsync(tm => tm.TechniqueId == techniqueId && tm.MediaAssetId == mediaAssetId);

        if (entry != null)
        {
            entry.IsCover = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<UserTechniqueResource> AddPersonalResourceAsync(int userId, int techniqueId, int mediaAssetId, string? notes, int displayOrder)
    {
        var resource = new UserTechniqueResource
        {
            UserId = userId,
            TechniqueId = techniqueId,
            MediaAssetId = mediaAssetId,
            PersonalNotes = notes,
            DisplayOrder = displayOrder
        };

        _context.UserTechniqueResources.Add(resource);
        await _context.SaveChangesAsync();
        return resource;
    }

    public async Task<UserProfileMedia> AddProfileMediaAsync(int userId, int mediaAssetId, bool isProfilePicture)
    {
        var media = new UserProfileMedia
        {
            UserId = userId,
            MediaAssetId = mediaAssetId,
            IsProfilePicture = isProfilePicture
        };

        _context.UserProfileMedia.Add(media);
        await _context.SaveChangesAsync();
        return media;
    }

    public async Task SetProfilePictureAsync(int userId, int mediaAssetId)
    {
        // Unset current profile picture
        var current = await _context.UserProfileMedia
            .Where(pm => pm.UserId == userId && pm.IsProfilePicture)
            .ToListAsync();

        foreach (var item in current)
        {
            item.IsProfilePicture = false;
        }

        // Check if this media is already associated
        var existing = await _context.UserProfileMedia
            .FirstOrDefaultAsync(pm => pm.UserId == userId && pm.MediaAssetId == mediaAssetId);

        if (existing != null)
        {
            existing.IsProfilePicture = true;
        }
        else
        {
            _context.UserProfileMedia.Add(new UserProfileMedia
            {
                UserId = userId,
                MediaAssetId = mediaAssetId,
                IsProfilePicture = true
            });
        }

        await _context.SaveChangesAsync();
    }
}
